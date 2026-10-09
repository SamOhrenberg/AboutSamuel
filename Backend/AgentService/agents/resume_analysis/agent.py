"""
Resume analysis: compares the official resume PDF against the site's work experience,
projects, and information, and proposes edits for Samuel to approve. A fixed LangGraph
pipeline, not a ReAct agent:

  load_resume -> load_site_data -> compare -> validate

The LLM only proposes. It sees short aliases (W1, P2, I3) rather than ids, it can't call
tools, and code validates every proposal before it is saved as pending: aliases must exist,
the supporting quote must really be in the resume, and only fields that differ are kept.
Nothing touches the site data until an admin approves a suggestion.
"""
import asyncio
import io
import re
import warnings
from collections.abc import AsyncIterator
from functools import lru_cache

import structlog
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_openai import AzureChatOpenAI
from langgraph.graph import END, StateGraph
from pypdf import PdfReader

from agents.resume_analysis import store
from agents.resume_analysis.prompts import COMPARE_INFORMATION_PROMPT, COMPARE_PROJECTS_PROMPT, COMPARE_WORK_PROMPT
from agents.resume_analysis.state import (
    PRESENT,
    InformationComparison,
    ListEdit,
    ProjectComparison,
    ResumeAnalysisState,
    SiteData,
    Suggestion,
    WorkComparison,
)
from config import get_settings
from tools.llm_retry import with_rate_limit_retry

logger = structlog.get_logger()

# langchain-openai 0.2.14 warns on every Pydantic structured output call. Same noise as Job Fit.
warnings.filterwarnings("ignore", message="Streaming with Pydantic response_format not yet supported")

STEPS = ["load_resume", "load_site_data", "compare", "validate"]

MIN_RESUME_CHARS = 200      # less than this and the PDF is probably a scan with no text layer
MAX_RESUME_CHARS = 40_000   # far beyond any resume; guards the prompt size
MAX_SUGGESTIONS = 40
MIN_EVIDENCE_CHARS = 20     # letters and digits in a quote, so "Python" alone can't count as evidence

# Same list as agents/adversarial/judge.py: these only accept their default temperature
REASONING_MODEL_PREFIXES = ("gpt-5", "o1", "o3", "o4")


@lru_cache()
def _llm() -> AzureChatOpenAI:
    """The stronger judge deployment when configured. This runs rarely and a wrong proposal
    costs more than a slow run, so it isn't the cheap chat model."""
    settings = get_settings()
    deployment = settings.azure_openai_judge_deployment or settings.azure_openai_chat_deployment
    if deployment.lower().startswith(REASONING_MODEL_PREFIXES):
        kwargs = {"temperature": 1}  # reasoning models want max_completion_tokens, which 0.2.14 doesn't send
    else:
        kwargs = {"temperature": 0, "max_tokens": 8000}
    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=deployment,
        api_version=settings.azure_openai_judge_api_version or settings.azure_openai_api_version,
        **kwargs,
    )


def analysis_model_name() -> str:
    settings = get_settings()
    return settings.azure_openai_judge_deployment or settings.azure_openai_chat_deployment


# ---- load_resume ----------------------------------------------------------

def extract_text(pdf: bytes) -> str:
    """Text of every page. The PDF's bullets and dashes often come out as U+FFFD."""
    reader = PdfReader(io.BytesIO(pdf))
    text = "\n".join((page.extract_text() or "") for page in reader.pages)
    text = re.sub(r"(?m)^\s*�\s*", "• ", text)  # a bullet at the start of a line
    text = text.replace("�", "-")                 # a dash or a middot between words
    return re.sub(r"[ \t]+\n", "\n", text).strip()


async def load_resume(state: ResumeAnalysisState) -> ResumeAnalysisState:
    file = await store.get_resume_file(state.get("resume_file_id"))
    text = extract_text(file["content"])
    if len(text) < MIN_RESUME_CHARS:
        raise ValueError("No readable text in that PDF. It may be a scan; upload a PDF with selectable text.")
    if len(text) > MAX_RESUME_CHARS:
        logger.warning("resume_text_truncated", chars=len(text))
        text = text[:MAX_RESUME_CHARS]
    await store.save_extracted_text(file["id"], text)
    logger.info("resume_text_extracted", file=file["name"], chars=len(text))
    return {"resume_file_id": file["id"], "resume_text": text}


# ---- load_site_data -------------------------------------------------------

async def load_site_data(state: ResumeAnalysisState) -> ResumeAnalysisState:
    site = await store.load_site_data()
    logger.info("resume_site_data_loaded", work=len(site["work"]), projects=len(site["projects"]),
                information=len(site["information"]))
    return {"site": site}


# ---- compare --------------------------------------------------------------

def _years(row: dict) -> str:
    return f"{row['start_year'] or '?'}-{row['end_year'] or PRESENT}"


def _render_work(site: SiteData) -> str:
    blocks = []
    for alias, r in site["work"].items():
        bullets = "".join(f"\n    - {a}" for a in r["achievements"]) or " (none)"
        blocks.append(f"{alias} | {r['employer']} | {r['title']} | {_years(r)}\n"
                      f"  summary: {r['summary'] or '(none)'}\n  achievements:{bullets}")
    return "\n\n".join(blocks) or "(no rows)"


def _render_projects(site: SiteData) -> str:
    blocks = []
    for alias, r in site["projects"].items():
        blocks.append(
            f"{alias} | {r['title']} | role: {r['role']} | {_years(r)}\n"
            f"  summary: {r['summary']}\n  detail: {r['detail'] or '(none)'}\n"
            f"  impact: {r['impact_statement'] or '(none)'}\n  tech stack: {', '.join(r['tech_stack']) or '(none)'}")
    return "\n\n".join(blocks) or "(no rows)"


def _render_information(site: SiteData) -> str:
    return "\n\n".join(
        f"{alias} (keywords: {', '.join(r['keywords']) or 'none'})\n{r['text']}"
        for alias, r in site["information"].items()) or "(no rows)"


async def _propose(prompt: str, schema, resume_text: str, section_tag: str, rendered: str, label: str):
    llm = _llm().with_structured_output(schema, method="json_schema", strict=True)
    message = f"<resume>\n{resume_text}\n</resume>\n\n<{section_tag}>\n{rendered}\n</{section_tag}>"
    result = await with_rate_limit_retry(
        lambda: llm.ainvoke([SystemMessage(content=prompt), HumanMessage(content=message)]), f"resume:{label}")
    return result.suggestions


async def compare(state: ResumeAnalysisState) -> ResumeAnalysisState:
    """Three independent comparisons, one per section, so each prompt stays focused."""
    text, site = state["resume_text"], state["site"]
    work, projects, information = await asyncio.gather(
        _propose(COMPARE_WORK_PROMPT, WorkComparison, text, "site_work_experience", _render_work(site), "work"),
        _propose(COMPARE_PROJECTS_PROMPT, ProjectComparison, text, "site_projects", _render_projects(site), "projects"),
        _propose(COMPARE_INFORMATION_PROMPT, InformationComparison, text, "site_information",
                 _render_information(site), "information"),
    )
    logger.info("resume_proposals", work=len(work), projects=len(projects), information=len(information))
    return {"proposals": {"work": work, "projects": projects, "information": information}}


# ---- validate -------------------------------------------------------------

# Per entity: the fields a suggestion may touch (camelCase is how they're stored and sent
# to the C# API), what an add needs, and how to label a row
SECTIONS = {
    "work": {
        "entity_type": "WorkExperience",
        "fields": {"employer": "employer", "title": "title", "start_year": "startYear", "end_year": "endYear",
                   "summary": "summary", "achievements": "achievements"},
        "required": ("employer", "title"),
        "label": lambda r: f"{r['employer']}: {r['title']}",
        "identity": lambda r: (_squash(r["employer"]), _squash(r["title"])),
    },
    "projects": {
        "entity_type": "Project",
        "fields": {"title": "title", "role": "role", "summary": "summary", "detail": "detail",
                   "impact_statement": "impactStatement", "tech_stack": "techStack", "start_year": "startYear",
                   "end_year": "endYear"},
        "required": ("title", "role", "summary"),
        "label": lambda r: r["title"],
        "identity": lambda r: _squash(r["title"]),
    },
    "information": {
        "entity_type": "Information",
        "fields": {"text": "text", "keywords": "keywords"},
        "required": ("text",),
        "label": lambda r: (r["text"] or "")[:70].replace("\n", " "),
        "identity": lambda r: _squash(r["text"]),
    },
}
YEAR_FIELDS = ("start_year", "end_year")


def _squash(value: str | None) -> str:
    """Lowercase letters and digits only, so formatting and line wraps can't hide a match."""
    return re.sub(r"[\W_]+", "", (value or "").lower())


def _clean(field: str, value):
    """A proposed value in the form the database stores, or None for 'no value'."""
    if isinstance(value, list):
        items = [v.strip() for v in value if v and v.strip()]
        return items or None
    if isinstance(value, str):
        value = value.strip()
        if field in YEAR_FIELDS:
            return None if not value or value.lower() == PRESENT else value
        return value or None
    return value


def _same(field: str, a, b) -> bool:
    if isinstance(a, list) or isinstance(b, list):
        # Order and case don't matter for technology and keyword lists, they do for bullets
        norm = (lambda xs: sorted(_squash(x) for x in xs or [])) if field in ("tech_stack", "keywords") \
            else (lambda xs: [_squash(x) for x in xs or []])
        return norm(a) == norm(b)
    return _squash(a) == _squash(b)


def _apply_list_edit(current: list[str], edit: ListEdit) -> list[str]:
    """The current list with the removals taken out and the additions appended. Removals only
    count when they match an existing entry, so the model can't delete what it can't name."""
    remove = {_squash(r) for r in edit.remove}
    result = [x for x in current if _squash(x) not in remove]
    have = {_squash(x) for x in result}
    for entry in edit.add:
        entry = entry.strip()
        if entry and _squash(entry) not in have:
            result.append(entry)
            have.add(_squash(entry))
    return result


def _validate_section(key: str, proposals: list, site: SiteData, resume_squashed: str, dropped: list[str]) -> list[Suggestion]:
    spec = SECTIONS[key]
    rows: dict[str, dict] = site[key]
    existing = {spec["identity"](r) for r in rows.values()}
    used_targets: set[str] = set()
    out: list[Suggestion] = []

    for p in proposals:
        def drop(why: str) -> None:
            dropped.append(f"{key}: {why}")
            logger.info("resume_proposal_dropped", section=key, why=why, target=p.target)

        if len(_squash(p.evidence)) < MIN_EVIDENCE_CHARS or _squash(p.evidence) not in resume_squashed:
            drop("evidence is not in the resume")
            continue

        current = None
        if p.action == "update":
            current = rows.get(p.target or "")
            if current is None:
                drop("unknown target")
                continue
            if p.target in used_targets:
                drop("second suggestion for the same row")
                continue

        # null (or blank) from the model means "leave as is". The one deliberate "no value" is
        # end_year 'present', which _clean turns into None.
        proposed = {}
        for f in spec["fields"]:
            raw = getattr(p.fields, f)
            if isinstance(raw, ListEdit):
                value = _apply_list_edit(current[f] if current else [], raw) or None
            else:
                value = _clean(f, raw)
            if value is not None or (f == "end_year" and isinstance(raw, str) and raw.strip().lower() == PRESENT):
                proposed[f] = value

        if current is not None:
            changes = {spec["fields"][f]: {"from": current[f], "to": v}
                       for f, v in proposed.items() if not _same(f, current[f], v)}
            if not changes:
                drop("no real difference")
                continue
            used_targets.add(p.target)
            out.append(Suggestion(action="update", entity_type=spec["entity_type"], entity_id=current["id"],
                                  label=spec["label"](current), changes=changes,
                                  rationale=p.rationale.strip(), evidence=" ".join(p.evidence.split())))
        else:
            if any(proposed.get(f) is None for f in spec["required"]):
                drop("add is missing required fields")
                continue
            if spec["identity"](proposed) in existing:
                drop("already on the site")
                continue
            existing.add(spec["identity"](proposed))
            out.append(Suggestion(action="add", entity_type=spec["entity_type"], entity_id=None,
                                  label=spec["label"](proposed),
                                  changes={spec["fields"][f]: {"from": None, "to": v} for f, v in proposed.items()},
                                  rationale=p.rationale.strip(), evidence=" ".join(p.evidence.split())))
    return out


async def validate(state: ResumeAnalysisState) -> ResumeAnalysisState:
    resume_squashed = _squash(state["resume_text"])
    dropped: list[str] = []
    suggestions: list[Suggestion] = []
    for key in SECTIONS:
        suggestions += _validate_section(key, state["proposals"][key], state["site"], resume_squashed, dropped)
    # Updates before adds, each in site order (the sort is stable)
    suggestions.sort(key=lambda s: s.action != "update")
    logger.info("resume_suggestions_validated", kept=len(suggestions[:MAX_SUGGESTIONS]), dropped=len(dropped))
    return {"suggestions": suggestions[:MAX_SUGGESTIONS], "dropped": dropped}


# ---- graph + runner -------------------------------------------------------

def build_graph():
    graph = StateGraph(ResumeAnalysisState)
    graph.add_node("load_resume", load_resume)
    graph.add_node("load_site_data", load_site_data)
    graph.add_node("compare", compare)
    graph.add_node("validate", validate)

    graph.set_entry_point("load_resume")
    graph.add_edge("load_resume", "load_site_data")
    graph.add_edge("load_site_data", "compare")
    graph.add_edge("compare", "validate")
    graph.add_edge("validate", END)
    return graph.compile()


resume_analysis_graph = build_graph()


def _summary(suggestions: list[Suggestion]) -> str:
    if not suggestions:
        return "The site data already agrees with the resume."
    updates = sum(s.action == "update" for s in suggestions)
    adds = len(suggestions) - updates
    parts = [f"{n} {word}{'' if n == 1 else 's'}" for n, word in ((updates, "update"), (adds, "addition")) if n]
    return " and ".join(parts) + " suggested."


async def analyze(file_id: str | None = None, save: bool = True) -> AsyncIterator[dict]:
    """
    Runs the analysis and yields progress events:
      {"analysisId": ...}   the run was recorded (only when saving)
      {"step": "<node>"}    a step started
      {"suggestions": [...], "summary": "...", "dropped": n}   the result
      {"error": "..."}      something failed
      {"done": True}        always last
    With save=False no analysis or suggestions are recorded, for trying it from the command line.
    """
    analysis_id = None
    finished = False
    try:
        if save:
            file = await store.get_resume_file(file_id)
            file_id = file["id"]
            analysis_id = await store.start_analysis(file_id, analysis_model_name())
            yield {"analysisId": analysis_id}

        state: dict = {}
        async for event in resume_analysis_graph.astream_events({"resume_file_id": file_id}, version="v2"):
            node = event.get("metadata", {}).get("langgraph_node")
            # Node runs are chains named after the node. Inner runnables share the node
            # metadata but have other names.
            if event["name"] in STEPS and node == event["name"]:
                if event["event"] == "on_chain_start":
                    yield {"step": node}
                elif event["event"] == "on_chain_end":
                    state.update(event["data"].get("output") or {})

        suggestions: list[Suggestion] = state["suggestions"]
        summary = _summary(suggestions)
        if analysis_id:
            await store.finish_analysis(analysis_id, suggestions, summary)
        finished = True
        yield {"suggestions": [s.model_dump() for s in suggestions], "summary": summary,
               "dropped": len(state.get("dropped", []))}

    except (store.AnalysisBusy, LookupError) as e:
        finished = True
        if analysis_id:  # AnalysisBusy comes before a run exists; a missing file can come after
            await store.fail_analysis(analysis_id, str(e))
        yield {"error": str(e)}
    except asyncio.CancelledError:
        if analysis_id and not finished:
            await store.fail_analysis(analysis_id, "Cancelled: the connection closed before it finished.")
        raise
    except Exception as e:
        logger.error("resume_analysis_failed", error=str(e), error_type=type(e).__name__, exc_info=True)
        finished = True
        if analysis_id:
            await store.fail_analysis(analysis_id, f"{type(e).__name__}: {e}")
        yield {"error": str(e) if isinstance(e, ValueError) else "The analysis failed. Check the agent logs."}

    yield {"done": True}


if __name__ == "__main__":
    # Try it without saving anything, against the current resume and the local database:
    #   python -m agents.resume_analysis.agent
    import json

    from database.connection import close_pool

    async def _try() -> None:
        try:
            async for event in analyze(save=False):
                if "suggestions" in event:
                    print(f"\n{event['summary']} ({event['dropped']} proposals dropped in validation)")
                    for s in event["suggestions"]:
                        print(f"\n[{s['action'].upper()}] {s['entity_type']}: {s['label']}")
                        print(f"  why: {s['rationale']}\n  quote: {s['evidence']!r}")
                        for field, change in s["changes"].items():
                            print(f"  {field}: {json.dumps(change['from'], ensure_ascii=False)[:160]}"
                                  f"\n      -> {json.dumps(change['to'], ensure_ascii=False)[:300]}")
                else:
                    print(event)
        finally:
            await close_pool()

    asyncio.run(_try())
