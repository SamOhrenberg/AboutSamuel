import asyncio
from collections.abc import Awaitable, Callable
from typing import TypeVar

import openai
import structlog

logger = structlog.get_logger()

T = TypeVar("T")

MAX_ATTEMPTS = 5


async def with_rate_limit_retry(call: Callable[[], Awaitable[T]], label: str) -> T:
    """
    For background work that makes many LLM calls on the shared gpt-4.1-mini deployment
    (adversarial runs, recruiter triage backfills) and can hit its tokens-per-minute
    quota. The OpenAI client only retries a 429 twice, quickly. Background work can
    afford to wait: honor Retry-After, back off, and only give up after several tries.
    """
    for attempt in range(1, MAX_ATTEMPTS + 1):
        try:
            return await call()
        except openai.RateLimitError as e:
            if attempt == MAX_ATTEMPTS:
                raise
            retry_after = e.response.headers.get("retry-after") if e.response is not None else None
            try:
                wait = float(retry_after)
            except (TypeError, ValueError):
                wait = 5 * 2 ** (attempt - 1)
            wait = min(max(wait, 2), 60)
            logger.warning("adversarial_rate_limited", call=label, attempt=attempt, wait_seconds=wait)
            await asyncio.sleep(wait)
    raise RuntimeError("unreachable")
