import json
import uuid

from fastapi import APIRouter, Depends
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

from agents.resume_analysis.agent import analyze
from api.adversarial import require_internal_secret

router = APIRouter(prefix="/resume-analysis", tags=["resume-analysis"],
                   dependencies=[Depends(require_internal_secret)])


class AnalyzeRequest(BaseModel):
    # C# sends camelCase, Python code uses snake_case
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    resume_file_id: uuid.UUID | None = None  # null means the current resume


def _sse(payload: dict) -> str:
    return f"data: {json.dumps(payload)}\n\n"


@router.post("/stream")
async def analyze_stream(request: AnalyzeRequest):
    """
    Runs the resume analysis and streams progress as SSE. Called by the C# admin API.
      {"analysisId": "..."}                                  the run was recorded
      {"step": "<node>"}                                     a step started
      {"result": {"summary", "suggestionCount", "dropped"}}  finished; the suggestions are in the database
      {"error": "..."}                                       something failed
      {"done": true}                                         always last
    """

    async def generate():
        async for event in analyze(str(request.resume_file_id) if request.resume_file_id else None):
            if "suggestions" in event:
                event = {"result": {"summary": event["summary"], "suggestionCount": len(event["suggestions"]),
                                    "dropped": event["dropped"]}}
            yield _sse(event)

    return StreamingResponse(
        generate(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
