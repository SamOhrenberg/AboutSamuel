from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
import json
import structlog
from agents.samuellm.agent import stream_chat, query_chat

router = APIRouter(prefix="/chat", tags=["chat"])
logger = structlog.get_logger()


class HistoryMessage(BaseModel):
    role: str
    content: str


class ChatRequest(BaseModel):
    message: str
    history: list[HistoryMessage] = []
    session_tracking_id: str | None = None


@router.post("/stream")
async def stream_chat_endpoint(request: ChatRequest):
    """
    Streaming chat endpoint. Returns SSE stream of tokens followed by a meta event.
    The C# API proxies this endpoint and forwards the stream to the frontend.
    """
    history = [{"role": m.role, "content": m.content} for m in request.history]

    async def generate():
        try:
            async for chunk in stream_chat(history, request.message):
                yield f"data: {json.dumps(chunk)}\n\n"
        except Exception as e:
            logger.error("stream_chat_endpoint_error", error=str(e))
            yield f"data: {json.dumps({'token': 'An error occurred. Please try again.'})}\n\n"
            yield f"data: {json.dumps({'meta': {'error': True}})}\n\n"

    return StreamingResponse(
        generate(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
        },
    )


@router.post("/query")
async def query_chat_endpoint(request: ChatRequest):
    """Non-streaming chat endpoint for internal use by background agents."""
    history = [{"role": m.role, "content": m.content} for m in request.history]
    result = await query_chat(history, request.message)
    return result
