"""Prompt Shields (Azure AI Content Safety) for third-party text an LLM is about to read.

Text goes in `documents`, which runs the indirect attack detector: instructions hidden in
content the model processes, like a recruiter email telling "the AI assistant" to do
something. `userPrompt` is the direct jailbreak detector for what a user types; the chat
deployment's content filter already covers that, so it's left out.

Auth is the same Entra token as the model calls. The caller needs the custom
"AboutSamuel Prompt Shields User" role on the Foundry account (infra/bootstrap).

Billed per 1,000-character text record, so callers should shield exactly the text the
model will see, not more.
"""
from dataclasses import dataclass
from typing import Literal

import httpx
import structlog
from azure.core.exceptions import ClientAuthenticationError
from opentelemetry import trace

from config import get_settings
from llm import token_provider

logger = structlog.get_logger()
tracer = trace.get_tracer(__name__)

API_VERSION = "2024-09-01"
# Service limit for a single shieldPrompt request.
MAX_CHARS = 10_000

Verdict = Literal["attack", "clean", "unavailable"]


@dataclass(frozen=True)
class ShieldResult:
    verdict: Verdict

    @property
    def attack_detected(self) -> bool:
        return self.verdict == "attack"


async def shield_document(client: httpx.AsyncClient, text: str, label: str) -> ShieldResult:
    """Never raises. "unavailable" means the check couldn't run; the caller decides
    whether that fails open or closed. `label` identifies the item in logs (no content)."""
    url = f"{get_settings().azure_openai_endpoint.rstrip('/')}/contentsafety/text:shieldPrompt"
    with tracer.start_as_current_span("prompt_shields") as span:
        span.set_attribute("app.prompt_shields.chars", min(len(text), MAX_CHARS))
        try:
            response = await client.post(
                url,
                params={"api-version": API_VERSION},
                headers={"Authorization": f"Bearer {token_provider()()}"},
                json={"documents": [text[:MAX_CHARS]]},
                timeout=10,
            )
            response.raise_for_status()
            detected = response.json()["documentsAnalysis"][0]["attackDetected"]
        except (httpx.HTTPError, ClientAuthenticationError, KeyError, IndexError, ValueError) as e:
            # The response body is the service's error message, never the document.
            status = getattr(getattr(e, "response", None), "status_code", None)
            logger.warning("prompt_shields_unavailable", item=label, status=status, error_type=type(e).__name__)
            span.set_attribute("app.prompt_shields.verdict", "unavailable")
            return ShieldResult("unavailable")

        verdict: Verdict = "attack" if detected else "clean"
        span.set_attribute("app.prompt_shields.verdict", verdict)
        if detected:
            logger.warning("prompt_shields_attack_detected", item=label)
        return ShieldResult(verdict)
