import asyncio
import warnings
from datetime import datetime
from functools import lru_cache

import structlog
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_openai import AzureChatOpenAI
from langgraph.graph import END, StateGraph

from agents.job_fit.prompts import ASSESS_FIT_PROMPT, COVER_LETTER_PROMPT, EXTRACT_REQUIREMENTS_PROMPT
from agents.job_fit.state import (
    Assessment,
    Citation,
    Evidence,
    FitRating,
    JobFitState,
    LlmFitAssessment,
    Requirement,
    RequirementsExtraction,
)
from config import get_settings
from tools.retrieval import embed_queries, get_career_timeline, search_pgvector, with_tech_stack

logger = structlog.get_logger()

# langchain-openai 0.2.14 warns on every Pydantic structured output call, then
# correctly falls back to not streaming. Nothing is wrong, it's just noise.
warnings.filterwarnings("ignore", message="Streaming with Pydantic response_format not yet supported")

MAX_REQUIREMENTS = 12

# Retrieval is tuned for recall: scores from text-embedding-3-small bunch up
# between ~0.2 and ~0.55, so a strict cutoff drops real matches (AboutSamuel.com
# scored below a generic note for "LLM experience"). The floor only removes
# noise. assess_fit decides what actually counts as evidence.
EVIDENCE_PER_REQUIREMENT = 4
MIN_EVIDENCE_SCORE = 0.2

# Keeps the assess_fit prompt a sane size with 12 requirements x 4 hits
MAX_EVIDENCE_CHARS = 700

# Fit score weights: must-haves count double, a partial match counts half
IMPORTANCE_WEIGHT = {"must_have": 2.0, "nice_to_have": 1.0}
STATUS_VALUE = {"strong": 1.0, "partial": 0.5, "no_evidence": 0.0}
RATING_BANDS: list[tuple[float, FitRating]] = [
    (0.8, "strong_fit"),
    (0.6, "good_fit"),
    (0.4, "partial_fit"),
    (0.0, "weak_fit"),
]


@lru_cache()
def _llm(max_tokens: int) -> AzureChatOpenAI:
    settings = get_settings()
    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=settings.azure_openai_chat_deployment,
        api_version=settings.azure_openai_api_version,
        max_tokens=max_tokens,
        temperature=0,
    )


@lru_cache()
def _writer_llm() -> AzureChatOpenAI:
    """Plain-text model for the cover letter. A little temperature so it reads like a
    person wrote it, and streaming so the page can show it being written."""
    settings = get_settings()
    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=settings.azure_openai_chat_deployment,
        api_version=settings.azure_openai_api_version,
        max_tokens=800,
        temperature=0.5,
        streaming=True,
    )


async def extract_requirements(state: JobFitState) -> JobFitState:
    """Pull structured requirements out of the job description, or flag that it isn't one."""
    llm = _llm(1500).with_structured_output(RequirementsExtraction, method="json_schema", strict=True)

    result: RequirementsExtraction = await llm.ainvoke([
        SystemMessage(content=EXTRACT_REQUIREMENTS_PROMPT),
        HumanMessage(content=f"<job_description>\n{state['job_description']}\n</job_description>"),
    ])

    # Ids come from code, not the LLM, so later steps can cite them reliably
    requirements = [
        Requirement(id=f"r{i}", **r.model_dump())
        for i, r in enumerate(result.requirements[:MAX_REQUIREMENTS], start=1)
    ]

    logger.info(
        "job_fit_requirements_extracted",
        is_job_description=result.is_job_description,
        job_title=result.job_title,
        count=len(requirements),
    )

    return {
        "is_job_description": result.is_job_description,
        "job_title": result.job_title,
        "company": result.company,
        "requirements": requirements,
    }



async def retrieve_evidence(state: JobFitState) -> JobFitState:
    """Find the closest pieces of Samuel's history for each requirement. No LLM involved."""
    requirements = state["requirements"]
    if not requirements:
        return {"evidence": {}}

    # Keywords alone embed poorly ("Python" matched nothing useful). The full
    # requirement plus keywords ranked the right projects noticeably higher.
    vectors = await embed_queries([f"{r.text}. {r.search_query}" for r in requirements])
    results = await asyncio.gather(*(
        search_pgvector(vector, limit=EVIDENCE_PER_REQUIREMENT) for vector in vectors
    ))

    evidence: dict[str, list[Evidence]] = {}
    for requirement, rows in zip(requirements, results):
        evidence[requirement.id] = [
            Evidence(
                id=f"{row['entity_type']}:{row['id']}",
                entity_type=row["entity_type"],
                label=row["sub_label"],
                content=with_tech_stack(row["content"], row["tech_stack"]),
                score=round(float(row["score"]), 3),
            )
            for row in rows
            if row["score"] >= MIN_EVIDENCE_SCORE
        ]

    logger.info(
        "job_fit_evidence_retrieved",
        requirements=len(requirements),
        hits=sum(len(v) for v in evidence.values()),
        empty=[rid for rid, hits in evidence.items() if not hits],
    )
    return {"evidence": evidence}


async def assess_fit(state: JobFitState) -> JobFitState:
    """Rate every requirement against its evidence in one LLM call, then validate the citations."""
    requirements = state["requirements"]
    evidence = state["evidence"]

    # Short aliases instead of GUID ids (LLMs mistype GUIDs), and each piece of
    # evidence listed once even when several requirements matched it
    alias_of: dict[str, str] = {}
    catalog: list[str] = []
    for requirement in requirements:
        for e in evidence.get(requirement.id, []):
            if e.id not in alias_of:
                alias_of[e.id] = f"E{len(alias_of) + 1}"
                label = f": {e.label}" if e.label else ""
                catalog.append(f"{alias_of[e.id]} ({e.entity_type}{label})\n{e.content[:MAX_EVIDENCE_CHARS]}")
    real_id_of = {alias: real for real, alias in alias_of.items()}

    requirement_lines = [
        f"{r.id} [{r.importance}] {r.text}\n"
        f"  candidate evidence: {', '.join(alias_of[e.id] for e in evidence.get(r.id, [])) or 'none'}"
        for r in requirements
    ]

    timeline = _format_timeline(await get_career_timeline())

    llm = _llm(3000).with_structured_output(LlmFitAssessment, method="json_schema", strict=True)
    result: LlmFitAssessment = await llm.ainvoke([
        SystemMessage(content=ASSESS_FIT_PROMPT),
        HumanMessage(content=(
            "CAREER TIMELINE\n\n" + timeline
            + "\n\nEVIDENCE CATALOG\n\n" + ("\n\n".join(catalog) or "(none)")
            + "\n\nREQUIREMENTS\n\n" + "\n".join(requirement_lines)
        )),
    ])

    # Validate: one verdict per requirement, citations must be evidence that was
    # actually retrieved for that requirement, and a match needs at least one citation
    verdicts = {}
    for a in result.assessments:
        verdicts.setdefault(a.requirement_id, a)

    evidence_by_id = {e.id: e for hits in evidence.values() for e in hits}
    assessments: list[Assessment] = []
    bad_citations: list[str] = []
    padded: list[str] = []
    downgraded: list[str] = []
    for requirement in requirements:
        verdict = verdicts.get(requirement.id)
        if verdict is None:
            downgraded.append(requirement.id)
            assessments.append(Assessment(
                requirement_id=requirement.id, status="no_evidence", citations=[],
                reason="This requirement wasn't assessed.",
            ))
            continue

        allowed = {alias_of[e.id] for e in evidence.get(requirement.id, [])}
        bad_citations += [f"{requirement.id}:{c.alias}" for c in verdict.citations if c.alias not in allowed]
        cited: list[Citation] = []
        for c in verdict.citations:
            if c.alias in allowed and all(x.evidence_id != real_id_of[c.alias] for x in cited):
                cited.append(Citation(evidence_id=real_id_of[c.alias], supports=c.supports))

        if requirement.category == "technical_skill":
            kept = _trim_padding(requirement, cited, evidence_by_id)
            padded += [f"{requirement.id}:{c.evidence_id}" for c in cited if c not in kept]
            cited = kept

        status, reason = verdict.status, verdict.reason
        if status == "no_evidence":
            cited = []
        elif not cited:
            # A match with nothing valid to back it up is exactly what we're guarding against
            downgraded.append(requirement.id)
            status = "no_evidence"
            reason = f"No supporting evidence in Samuel's portfolio was cited for: {requirement.text}."

        assessments.append(Assessment(
            requirement_id=requirement.id, status=status, citations=cited, reason=reason,
        ))

    fit_score, must_haves_met = _score(requirements, assessments)
    rating = next(r for floor, r in RATING_BANDS if fit_score >= floor)

    logger.info(
        "job_fit_assessed",
        rating=rating,
        fit_score=fit_score,
        must_haves_met=must_haves_met,
        bad_citations=bad_citations,
        padded=padded,
        downgraded=downgraded,
    )
    return {
        "assessments": assessments,
        "summary": result.summary,
        "rating": rating,
        "fit_score": fit_score,
        "must_haves_met": must_haves_met,
    }


async def write_cover_letter(state: JobFitState) -> JobFitState:
    """Write a cover letter from only the verified (strong or partial) matches."""
    requirement_of = {r.id: r for r in state["requirements"]}
    evidence_by_id = {e.id: e for hits in state["evidence"].values() for e in hits}

    # Must-haves first, strong before partial, so the letter leads with the best matches
    rank = {("must_have", "strong"): 0, ("must_have", "partial"): 1,
            ("nice_to_have", "strong"): 2, ("nice_to_have", "partial"): 3}
    verified = sorted(
        (a for a in state["assessments"] if a.status != "no_evidence"),
        key=lambda a: rank[(requirement_of[a.requirement_id].importance, a.status)],
    )
    if not verified:
        return {"cover_letter": None}

    blocks = []
    for a in verified:
        r = requirement_of[a.requirement_id]
        lines = [f"[{r.importance}, {a.status}] {r.text}", f"  Assessment: {a.reason}"]
        for c in a.citations:
            e = evidence_by_id[c.evidence_id]
            lines.append(f"  Evidence ({e.label or e.entity_type}): {c.supports}\n    {e.content[:400]}")
        blocks.append("\n".join(lines))

    response = await _writer_llm().ainvoke([
        SystemMessage(content=COVER_LETTER_PROMPT),
        HumanMessage(content=(
            f"Role: {state.get('job_title') or 'not stated'}\n"
            f"Company: {state.get('company') or 'not stated'}\n\n"
            "VERIFIED MATCHES\n\n" + "\n\n".join(blocks)
        )),
    ])
    letter = response.content.strip()
    logger.info("job_fit_cover_letter_written", words=len(letter.split()))
    return {"cover_letter": letter}


def _trim_padding(
    requirement: Requirement, cited: list[Citation], evidence_by_id: dict[str, Evidence]
) -> list[Citation]:
    """gpt-4.1-mini pads technical citations with whatever it was handed (a C# project
    cited for "Python"). If some cited evidence literally mentions the requirement's
    terms, drop the ones that don't. Never removes the last citation and never changes
    a verdict, so the worst case is leftover padding, not a hidden real skill."""
    terms = [t.lower() for t in requirement.search_query.split() if len(t) > 1]

    def mentions(c: Citation) -> bool:
        text = evidence_by_id[c.evidence_id].content.lower()
        return any(t in text or t.rstrip("s") in text for t in terms)

    matching = [c for c in cited if mentions(c)]
    return matching or cited


def _format_timeline(roles: list[dict]) -> str:
    """The full role list plus the career span, worked out in code because LLMs
    are bad at adding up date ranges."""
    if not roles:
        return "(no roles in the portfolio)"
    lines = [
        f"- {r['title']} at {r['employer']}: {r['start_year'] or '?'} - {r['end_year'] or 'present'}"
        for r in roles
    ]
    start_years = [int(r["start_year"]) for r in roles if (r["start_year"] or "").strip().isdigit()]
    if start_years:
        span = datetime.now().year - min(start_years)
        lines.append(f"Professional career span: {min(start_years)} to now, about {span} years.")
    return "\n".join(lines)


def _score(requirements: list[Requirement], assessments: list[Assessment]) -> tuple[float, str]:
    status_of = {a.requirement_id: a.status for a in assessments}
    total = sum(IMPORTANCE_WEIGHT[r.importance] for r in requirements)
    earned = sum(IMPORTANCE_WEIGHT[r.importance] * STATUS_VALUE[status_of[r.id]] for r in requirements)
    must = [r for r in requirements if r.importance == "must_have"]
    met = sum(1 for r in must if status_of[r.id] != "no_evidence")
    return (round(earned / total, 2) if total else 0.0), f"{met}/{len(must)}"


# ---- graph ----------------------------------------------------------------

# In run order. The endpoint uses these names for its progress events.
STEPS = ["extract_requirements", "retrieve_evidence", "assess_fit", "write_cover_letter"]


def _after_extract(state: JobFitState) -> str:
    """Stop early when the input isn't a job description (or nothing was found to assess)."""
    if state.get("is_job_description") and state.get("requirements"):
        return "retrieve_evidence"
    return END


def build_graph():
    graph = StateGraph(JobFitState)
    graph.add_node("extract_requirements", extract_requirements)
    graph.add_node("retrieve_evidence", retrieve_evidence)
    graph.add_node("assess_fit", assess_fit)
    graph.add_node("write_cover_letter", write_cover_letter)

    graph.set_entry_point("extract_requirements")
    graph.add_conditional_edges("extract_requirements", _after_extract, ["retrieve_evidence", END])
    graph.add_edge("retrieve_evidence", "assess_fit")
    graph.add_edge("assess_fit", "write_cover_letter")
    graph.add_edge("write_cover_letter", END)
    return graph.compile()


# Built once on import, like SamuelLM's graph. No LLM clients are created until a node runs.
job_fit_graph = build_graph()


if __name__ == "__main__":
    # Run the whole graph against a local job description:
    #   python -m agents.job_fit.agent path/to/job_description.txt
    import sys

    from database.connection import close_pool

    async def _try() -> None:
        text = open(sys.argv[1], encoding="utf-8").read() if len(sys.argv) > 1 else sys.stdin.read()
        try:
            state = await job_fit_graph.ainvoke({"job_description": text})
        finally:
            await close_pool()

        print(f"{state.get('job_title')} @ {state.get('company')} (job description: {state['is_job_description']})")
        if "assessments" not in state:
            return
        labels = {e.id: e.label or e.entity_type for hits in state["evidence"].values() for e in hits}
        requirement_of = {r.id: r for r in state["requirements"]}
        for a in state["assessments"]:
            r = requirement_of[a.requirement_id]
            print(f"\n{r.id} [{r.importance}] {a.status.upper()}: {r.text}\n    {a.reason}")
            for c in a.citations:
                print(f"      - {labels[c.evidence_id]}: {c.supports}")

        s = state["summary"]
        print(f"\n{state['rating']} (score {state['fit_score']}, must-haves {state['must_haves_met']})")
        print(f"{s.headline}\nStrengths:", *s.strengths, sep="\n  - ")
        print("Gaps:", *s.gaps, sep="\n  - ")
        print(f"\n--- cover letter ---\n{state['cover_letter']}")

    asyncio.run(_try())
