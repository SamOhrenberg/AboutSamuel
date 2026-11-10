"""OpenTelemetry tracing and logs to App Insights.

Call configure_telemetry() before anything creates the FastAPI app or an OpenAI client.

What gets traced:
  - Incoming requests (FastAPI). A request from the C# API
    carries a W3C traceparent header, so these spans join the C# trace.
  - Outgoing httpx calls (distro), i.e. callbacks to the C# API.
  - Every chat and embedding call (openai-v2): GenAI semantic conventions with model,
    token counts, latency and, because content capture is on, the prompt and response.
  - asyncpg queries, so pgvector retrieval shows up in the latency breakdown.

App Insights has local auth off, so the exporter signs in with an Entra token
(Monitoring Metrics Publisher). The connection string only says where to send.

Content capture is per process, so code that handles text we've promised not to keep
(recruiter emails) runs under @redact_content: its spans keep model, tokens and latency,
but lose prompt and response content before export.
"""
import contextvars
import functools
import os

from config import get_settings

SERVICE_NAME = "agent-service"

# Defaults, overridable from the environment (e.g. a Railway var) without a deploy.
_ENV_DEFAULTS = {
    # Latest GenAI semantic conventions: gen_ai.input.messages / gen_ai.output.messages.
    "OTEL_SEMCONV_STABILITY_OPT_IN": "gen_ai_latest_experimental",
    # Prompts and responses go on the span itself, not as separate log events, so each
    # LLM call is one row. App Insights also copies them into the AppGenAIContent table.
    "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT": "span_only",
    # Railway health checks aren't worth a trace each.
    "OTEL_PYTHON_FASTAPI_EXCLUDED_URLS": "health",
    # The distro also probes the Azure VM metadata endpoint at startup. Off Azure that
    # fails and is logged as 3 exceptions per start. "otel" only reads OTEL_RESOURCE_ATTRIBUTES.
    "OTEL_EXPERIMENTAL_RESOURCE_DETECTORS": "otel",
}

# Span attributes that carry prompt, response or tool content.
CONTENT_ATTRIBUTES = frozenset({
    "gen_ai.input.messages",
    "gen_ai.output.messages",
    "gen_ai.system_instructions",
    "gen_ai.tool.definitions",
    "gen_ai.tool.call.arguments",
    "gen_ai.tool.call.result",
})
_REDACT_MARKER = "app.content_redacted"
_redact = contextvars.ContextVar("telemetry_redact_content", default=False)


def redact_content(func):
    """Async decorator: spans created while func runs are exported without content."""
    @functools.wraps(func)
    async def wrapper(*args, **kwargs):
        token = _redact.set(True)
        try:
            return await func(*args, **kwargs)
        finally:
            _redact.reset(token)
    return wrapper


def _redaction_processor():
    from opentelemetry.sdk.trace import SpanProcessor

    class RedactContentProcessor(SpanProcessor):
        """Runs before the exporter's processor (the distro adds custom ones first).
        on_start reads the context flag, since on_end may not run in the same context."""

        def on_start(self, span, parent_context=None):
            if _redact.get():
                span.set_attribute(_REDACT_MARKER, True)

        def on_end(self, span):
            attrs = span.attributes
            if not attrs or not attrs.get(_REDACT_MARKER):
                return
            # A ReadableSpan is meant to be immutable and there's no public way to drop
            # attributes, so swap the private dict. opentelemetry-sdk is pinned in requirements.txt.
            span._attributes = {k: v for k, v in attrs.items() if k not in CONTENT_ATTRIBUTES}

    return RedactContentProcessor()


_configured = False


def configure_telemetry() -> bool:
    """Returns whether telemetry was turned on."""
    global _configured
    if _configured:
        return True

    connection_string = get_settings().applicationinsights_connection_string
    if not connection_string:
        return False

    for key, value in _ENV_DEFAULTS.items():
        os.environ.setdefault(key, value)

    # Imported here so a run without telemetry doesn't pay for loading them.
    from azure.identity import DefaultAzureCredential
    from azure.monitor.opentelemetry import configure_azure_monitor
    from opentelemetry.instrumentation.asyncpg import AsyncPGInstrumentor
    from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
    from opentelemetry.instrumentation.openai_v2 import OpenAIInstrumentor
    from opentelemetry.sdk.resources import Resource

    configure_azure_monitor(
        connection_string=connection_string,
        credential=DefaultAzureCredential(),
        # service.name becomes cloud_RoleName: the node name on the App Insights map.
        resource=Resource.create({"service.name": SERVICE_NAME}),
        # Only export our own logs (log_config.py forwards structlog here), not uvicorn's
        # or the Azure SDK's. Keeps volume well under the daily cap.
        logger_name=SERVICE_NAME,
        span_processors=[_redaction_processor()],
        # FastAPI is instrumented below instead, because the distro can't pass options.
        instrumentation_options={"fastapi": {"enabled": False}},
        # Default sampler is rate-limited to 5 traces/s, which is effectively 100% here.
    )
    # Without exclude_spans, every streamed SSE chunk becomes its own "http send" span
    # (about 70 per chat), which is noise and most of the volume.
    FastAPIInstrumentor().instrument(exclude_spans=["receive", "send"])
    OpenAIInstrumentor().instrument()
    AsyncPGInstrumentor().instrument()

    _configured = True
    return True
