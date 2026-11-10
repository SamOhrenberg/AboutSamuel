"""ToolNode that traces each tool call as a GenAI execute_tool span.

openai-v2 only traces model calls, so without this a tool's work (embedding, SQL, the
callback to the C# API) shows up as loose siblings under the request. The span is made
current while the tool runs, so those calls nest under it.
"""
import json
import os

from langgraph.prebuilt import ToolNode
from opentelemetry import trace
from opentelemetry.trace import SpanKind, Status, StatusCode

_tracer = trace.get_tracer(__name__)


def _capture_content() -> bool:
    # Same switch as the model spans (set in telemetry.py), so tool arguments and results
    # follow the same privacy rules, including @redact_content.
    value = os.environ.get("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "")
    return value.lower() in ("true", "span_only", "span_and_event")


class TracedToolNode(ToolNode):
    # langgraph 0.2.x has no hook around a tool call, so this overrides the private method
    # every async call goes through (langgraph is pinned). From 1.0, use awrap_tool_call.
    async def _arun_one(self, call, input_type, config):
        name = call["name"]
        attributes = {
            "gen_ai.operation.name": "execute_tool",
            "gen_ai.tool.name": name,
            "gen_ai.tool.type": "function",
        }
        if call.get("id"):
            # Matches the id in the model's tool call, which links the two spans.
            attributes["gen_ai.tool.call.id"] = call["id"]
        tool = self.tools_by_name.get(name)
        if tool is not None and tool.description:
            attributes["gen_ai.tool.description"] = tool.description
        if _capture_content():
            attributes["gen_ai.tool.call.arguments"] = json.dumps(call.get("args", {}))

        with _tracer.start_as_current_span(
            f"execute_tool {name}", kind=SpanKind.INTERNAL, attributes=attributes
        ) as span:
            message = await super()._arun_one(call, input_type, config)
            # ToolNode turns tool exceptions into an error ToolMessage instead of raising.
            if getattr(message, "status", None) == "error":
                span.set_attribute("error.type", "tool_error")
                span.set_status(Status(StatusCode.ERROR))
            if _capture_content() and isinstance(getattr(message, "content", None), str):
                span.set_attribute("gen_ai.tool.call.result", message.content)
            return message
