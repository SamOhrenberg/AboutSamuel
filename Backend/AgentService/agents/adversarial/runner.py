"""
Runs adversarial cases against a sandboxed copy of SamuelLM.

Same model, prompt, and tools as the live chat, except contact_samuel is a fake that
sends nothing. search_experience stays real (it only reads the database), so answers
are grounded exactly like they are on the site. Runs in-process, so tests never touch
the C# API's chat log or rate limits.
"""
import asyncio
import time
from collections.abc import Awaitable, Callable
from functools import lru_cache

import openai
import structlog
from langchain_core.messages import AIMessage, ToolMessage
from langchain_core.tools import StructuredTool

from tools.llm_retry import with_rate_limit_retry
from agents.adversarial.state import AdversarialCase, CaseResult, ToolCall
from agents.samuellm.agent import TOOLS, build_graph, build_input
from tools.contact import contact_samuel

logger = structlog.get_logger()

# Enough to finish ~30 cases in about a minute without tripping Azure rate limits
MAX_CONCURRENT_CASES = 4


async def _fake_contact_samuel(email: str, message: str = "") -> str:
    # Same reply the real tool gives on success, so SamuelLM behaves the same way.
    # The call itself still shows up in the result's tool_calls for the judge.
    return f"Contact request sent successfully from {email}."


# Identical name, description, and argument schema, so the LLM sees the same tool
_sandboxed_contact = StructuredTool.from_function(
    coroutine=_fake_contact_samuel,
    name=contact_samuel.name,
    description=contact_samuel.description,
    args_schema=contact_samuel.args_schema,
)


@lru_cache()
def _sandboxed_graph():
    tools = [_sandboxed_contact if t.name == contact_samuel.name else t for t in TOOLS]
    return build_graph(tools)


async def run_case(case: AdversarialCase) -> CaseResult:
    start = time.monotonic()
    graph_input = build_input(case.history, case.prompt)
    try:
        result = await with_rate_limit_retry(lambda: _sandboxed_graph().ainvoke(graph_input), f"samuellm:{case.id}")
    except openai.BadRequestError as e:
        elapsed = int((time.monotonic() - start) * 1000)
        if getattr(e, "code", None) == "content_filter":
            logger.info("adversarial_case_blocked_by_filter", case=case.id)
            return CaseResult(case=case, answer="", tool_calls=[], blocked_by_content_filter=True,
                              duration_ms=elapsed)
        logger.error("adversarial_case_failed", case=case.id, error=str(e), error_type=type(e).__name__)
        return CaseResult(case=case, answer="", tool_calls=[], error=f"{type(e).__name__}: {e}",
                          duration_ms=elapsed)
    except Exception as e:
        logger.error("adversarial_case_failed", case=case.id, error=str(e), error_type=type(e).__name__)
        return CaseResult(case=case, answer="", tool_calls=[], error=f"{type(e).__name__}: {e}",
                          duration_ms=int((time.monotonic() - start) * 1000))

    # Only this turn's messages: everything after the input history and question
    new_messages = result["messages"][len(graph_input["messages"]):]
    tool_calls = [
        ToolCall(name=call["name"], args=call["args"])
        for m in new_messages if isinstance(m, AIMessage)
        for call in m.tool_calls
    ]
    retrieved = [
        m.content for m in new_messages
        if isinstance(m, ToolMessage) and m.name == "search_experience" and isinstance(m.content, str)
    ]
    final = new_messages[-1] if new_messages else None
    answer = final.content if isinstance(final, AIMessage) else ""

    return CaseResult(case=case, answer=answer, tool_calls=tool_calls, retrieved_context=retrieved,
                      duration_ms=int((time.monotonic() - start) * 1000))


async def run_cases(
    cases: list[AdversarialCase],
    on_progress: Callable[[int], Awaitable[None]] | None = None,
) -> list[CaseResult]:
    """on_progress gets the number of cases finished so far, after each one."""
    semaphore = asyncio.Semaphore(MAX_CONCURRENT_CASES)
    finished = 0

    async def limited(case: AdversarialCase) -> CaseResult:
        nonlocal finished
        async with semaphore:
            result = await run_case(case)
        finished += 1
        if on_progress:
            await on_progress(finished)
        return result

    # gather keeps the input order, so results line up with cases
    return await asyncio.gather(*(limited(c) for c in cases))
