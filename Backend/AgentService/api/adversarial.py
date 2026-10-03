import asyncio

import structlog
from fastapi import APIRouter, Depends, Header, HTTPException

from agents.adversarial.agent import execute_stored_run
from agents.adversarial.judge import judge_model_name
from agents.adversarial.store import create_run
from config import get_settings

logger = structlog.get_logger()


def require_internal_secret(x_internal_secret: str | None = Header(default=None)) -> None:
    """This endpoint spends money (~100 LLM calls per run), so unlike chat it checks the
    shared secret the C# API sends with every request, even on the private network."""
    expected = get_settings().csharp_api_internal_secret
    if not expected or x_internal_secret != expected:
        raise HTTPException(status_code=401)


router = APIRouter(prefix="/adversarial", tags=["adversarial"], dependencies=[Depends(require_internal_secret)])

# The running task, kept referenced so it isn't garbage collected mid-run, and so a
# second request can't start a parallel run
_current_run: asyncio.Task | None = None


@router.post("/runs", status_code=202)
async def start_run():
    """Start a run in the background and return its id right away. Progress and results
    land in the AdversarialRuns tables, which the C# admin API reads."""
    global _current_run
    if _current_run and not _current_run.done():
        raise HTTPException(status_code=409, detail="A run is already in progress.")

    run_id = await create_run(target_model=get_settings().azure_openai_chat_deployment,
                              judge_model=judge_model_name())
    _current_run = asyncio.create_task(execute_stored_run(run_id))
    logger.info("adversarial_run_started", run_id=str(run_id))
    return {"runId": str(run_id)}
