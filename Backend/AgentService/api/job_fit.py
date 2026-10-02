import json

import structlog
from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from agents.job_fit.agent import STEPS, job_fit_graph

router = APIRouter(prefix="/job-fit", tags=["job-fit"])
logger = structlog.get_logger()

# A long real posting is ~6-8k characters. The C# API enforces the same cap.
MAX_JOB_DESCRIPTION_CHARS = 12_000


class JobFitRequest(BaseModel):
    # C# sends camelCase (jobDescription), Python code uses snake_case
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)

    job_description: str = Field(min_length=20, max_length=MAX_JOB_DESCRIPTION_CHARS)


def _sse(payload: dict) -> str:
    return f"data: {json.dumps(payload)}\n\n"


@router.post("/stream")
async def job_fit_stream(request: JobFitRequest):
    """
    Runs the Job Fit graph and streams progress as SSE:
      {"step": "<node>"}            a step started
      {"requirements": ...}         after extraction
      {"evidenceCount": n}         after retrieval
      {"assessment": ...}           ratings, citations, summary
      {"token": "..."}              cover letter, as it's written
      {"error": "..."}              not a job description, or something broke
      {"done": true}                always last
    """

    async def generate():
        state: dict = {}
        try:
            async for event in job_fit_graph.astream_events(
                {"job_description": request.job_description}, version="v2"
            ):
                kind = event["event"]
                node = event.get("metadata", {}).get("langgraph_node")

                # Node runs show up as chains named after the node. Inner runnables
                # share the node metadata but have other names, so match both.
                if kind == "on_chain_start" and event["name"] in STEPS and node == event["name"]:
                    yield _sse({"step": node})

                elif kind == "on_chain_end" and event["name"] in STEPS and node == event["name"]:
                    output = event["data"].get("output") or {}
                    state.update(output)
                    payload = _step_result(node, state)
                    if payload:
                        yield _sse(payload)

                elif kind == "on_chat_model_stream" and node == "write_cover_letter":
                    token = event["data"]["chunk"].content
                    if token:
                        yield _sse({"token": token})

        except Exception as e:
            logger.error("job_fit_stream_failed", error=str(e), error_type=type(e).__name__, exc_info=True)
            yield _sse({"error": "Something went wrong analyzing that job description. Please try again."})

        yield _sse({"done": True})

    return StreamingResponse(
        generate(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


def _step_result(node: str, state: dict) -> dict | None:
    """What the page gets when a step finishes, built from the state so far."""
    if node == "extract_requirements":
        if not state.get("is_job_description") or not state.get("requirements"):
            return {"error": "That doesn't look like a job description. Paste the full posting and try again."}
        return {
            "jobTitle": state.get("job_title"),
            "company": state.get("company"),
            "requirements": [r.model_dump() for r in state["requirements"]],
        }

    if node == "retrieve_evidence":
        return {"evidenceCount": sum(len(hits) for hits in state["evidence"].values())}

    if node == "assess_fit":
        # Ship citations with their label and type so the page can render links
        # without needing the whole evidence set
        evidence_by_id = {e.id: e for hits in state["evidence"].values() for e in hits}
        assessments = []
        for a in state["assessments"]:
            citations = []
            for c in a.citations:
                e = evidence_by_id[c.evidence_id]
                entity_type, entity_id = c.evidence_id.split(":", 1)
                citations.append({
                    "entityType": entity_type,
                    "entityId": entity_id,
                    "label": e.label,
                    "supports": c.supports,
                })
            assessments.append({
                "requirementId": a.requirement_id,
                "status": a.status,
                "reason": a.reason,
                "citations": citations,
            })
        return {
            "assessment": {
                "rating": state["rating"],
                "fitScore": state["fit_score"],
                "mustHavesMet": state["must_haves_met"],
                "summary": state["summary"].model_dump(),
                "requirements": assessments,
            }
        }

    if node == "write_cover_letter" and state.get("cover_letter") is None:
        return {"error": "Nothing in the portfolio matched well enough to write a cover letter."}

    return None
