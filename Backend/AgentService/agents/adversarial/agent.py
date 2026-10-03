import warnings
from functools import lru_cache

import structlog
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_openai import AzureChatOpenAI

from agents.adversarial.prompts import GENERATOR_PROMPT
from agents.adversarial.state import AdversarialCase, GeneratedCases
from agents.adversarial.suite import SUITE
from config import get_settings
from tools.retrieval import format_portfolio_overview, get_career_timeline, get_project_catalog

logger = structlog.get_logger()

# Same langchain-openai 0.2.14 noise as Job Fit: warns on every Pydantic structured
# output call, then correctly doesn't stream. Nothing is wrong.
warnings.filterwarnings("ignore", message="Streaming with Pydantic response_format not yet supported")

# Generated variations per run, on top of the fixed suite
GENERATED_PER_CATEGORY = {"twisted_fact": 3, "false_premise": 2, "fabricated_numbers": 2}


@lru_cache()
def _generator_llm() -> AzureChatOpenAI:
    """Creative on purpose: each run should probe something a little different."""
    settings = get_settings()
    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=settings.azure_openai_chat_deployment,
        api_version=settings.azure_openai_api_version,
        max_tokens=1500,
        temperature=0.9,
    )


async def generate_cases(overview: str) -> list[AdversarialCase]:
    """Fresh variations built from Samuel's real projects and employers."""
    llm = _generator_llm().with_structured_output(GeneratedCases, method="json_schema", strict=True)
    result: GeneratedCases = await llm.ainvoke([
        SystemMessage(content=GENERATOR_PROMPT.format(**GENERATED_PER_CATEGORY)),
        HumanMessage(content=overview),
    ])

    # Keep only as many per category as asked for, numbered so they're easy to refer to
    cases: list[AdversarialCase] = []
    counts: dict[str, int] = {}
    for g in result.cases:
        n = counts.get(g.category, 0)
        if n >= GENERATED_PER_CATEGORY[g.category]:
            continue
        counts[g.category] = n + 1
        cases.append(AdversarialCase(
            id=f"gen-{g.category}-{n + 1}",
            category=g.category,
            prompt=g.prompt,
            planted_claim=g.planted_claim,
            source="generated",
        ))

    logger.info("adversarial_cases_generated", counts=counts)
    return cases


async def build_case_set() -> tuple[list[AdversarialCase], str]:
    """The fixed suite plus this run's generated variations, and the portfolio overview
    they were built from (the judge needs the same overview)."""
    overview = format_portfolio_overview(await get_career_timeline(), await get_project_catalog())
    return SUITE + await generate_cases(overview), overview


async def run_adversarial_suite(on_total=None, on_answered=None, on_judged=None) -> tuple[list, dict]:
    """The whole thing: build cases, run them against sandboxed SamuelLM, judge, summarize.
    The optional callbacks report progress (case count, then cases answered/judged so far)."""
    from agents.adversarial.judge import judge_results, summarize
    from agents.adversarial.runner import run_cases

    cases, overview = await build_case_set()
    if on_total:
        await on_total(len(cases))
    results = await run_cases(cases, on_answered)
    verdicts = await judge_results(results, overview, on_judged)
    summary = summarize(verdicts)
    logger.info("adversarial_suite_finished", pass_rate=summary["pass_rate"],
                passed=summary["passed"], scored=summary["scored"])
    return verdicts, summary


async def execute_stored_run(run_id) -> None:
    """Runs the suite for a row already created in AdversarialRuns, keeping its progress
    current and saving every verdict. Failures are recorded on the run, never raised."""
    from agents.adversarial import store

    try:
        verdicts, summary = await run_adversarial_suite(
            on_total=lambda n: store.update_progress(run_id, total=n),
            on_answered=lambda n: store.update_progress(run_id, answered=n),
            on_judged=lambda n: store.update_progress(run_id, judged=n),
        )
        await store.complete_run(run_id, verdicts, summary)
    except Exception as e:
        logger.error("adversarial_run_failed", run_id=str(run_id), error=str(e),
                     error_type=type(e).__name__, exc_info=True)
        await store.fail_run(run_id, f"{type(e).__name__}: {e}")


if __name__ == "__main__":
    # Preview this run's cases, run them against SamuelLM, or run and judge them:
    #   python -m agents.adversarial.agent
    #   python -m agents.adversarial.agent --run
    #   python -m agents.adversarial.agent --judge
    import asyncio
    import sys

    from agents.adversarial.judge import judge_model_name
    from agents.adversarial.runner import run_cases
    from database.connection import close_pool

    async def _judge() -> None:
        try:
            verdicts, summary = await run_adversarial_suite()
        finally:
            await close_pool()
        print(f"Judge: {judge_model_name()}")
        print(f"PASS RATE: {summary['passed']}/{summary['scored']} ({summary['pass_rate']:.0%})")
        for category, c in summary["by_category"].items():
            extra = "".join(f", {n} {k}" for k, n in c.items() if k in ("invalid_case", "error") and n)
            print(f"  {category:<20} {c['pass']} pass, {c['fail']} fail{extra}")
        for v in verdicts:
            if v.verdict == "pass":
                continue
            print(f"\n[{v.result.case.id}] {v.verdict.upper()} {v.failure_type} ({v.severity}), "
                  f"premise {v.premise_handling}\n  Q: {v.result.case.prompt}\n"
                  f"  A: {' '.join(v.result.answer.split())[:200]}\n  why: {v.explanation}")
            for c in v.claims:
                if not c.supported:
                    print(f"    unsupported: {c.claim}")

    if "--judge" in sys.argv:
        asyncio.run(_judge())
        sys.exit()

    async def _try() -> None:
        try:
            cases, overview = await build_case_set()
            results = await run_cases(cases) if "--run" in sys.argv else None
        finally:
            await close_pool()

        if results is None:
            print(overview, "\n")
            for c in cases:
                planted = f"\n      planted: {c.planted_claim}" if c.planted_claim else ""
                print(f"[{c.id}] {c.category}: {c.prompt}{planted}")
            return

        for r in results:
            tools = ", ".join(t.name for t in r.tool_calls) or "-"
            answer = " ".join(r.answer.split())[:220]
            status = " | BLOCKED by Azure content filter" if r.blocked_by_content_filter else (
                f" | ERROR {r.error}" if r.error else "")
            print(f"\n[{r.case.id}] {r.case.prompt}\n  tools: {tools} | {r.duration_ms} ms{status}\n  > {answer}")

    asyncio.run(_try())
