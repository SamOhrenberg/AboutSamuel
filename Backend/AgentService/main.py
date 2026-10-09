import asyncio

import structlog
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from log_config import configure_logging, flush_logs

configure_logging()

from agents.adversarial.store import mark_interrupted_runs
from agents.recruiter.loop import run_forever as run_recruiter_triage
from api.adversarial import router as adversarial_router
from api.chat import router as chat_router
from api.health import router as health_router
from api.job_fit import router as job_fit_router
from api.recruiters import router as recruiters_router
from api.resume_analysis import router as resume_analysis_router
from database.connection import close_pool
from tools.resume import ensure_seeded as ensure_resume_seeded

logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("agent_service_starting")
    try:
        if interrupted := await mark_interrupted_runs():
            logger.warning("adversarial_runs_marked_interrupted", count=interrupted)
    except Exception as e:
        logger.warning("adversarial_cleanup_failed", error=str(e))
    try:
        if await ensure_resume_seeded():
            logger.info("resume_seeded_from_bundled_file")
    except Exception as e:
        logger.warning("resume_seed_failed", error=str(e))
    triage_task = asyncio.create_task(run_recruiter_triage())
    logger.info("agent_service_ready")

    yield

    logger.info("agent_service_shutting_down")
    triage_task.cancel()
    await close_pool()
    flush_logs()


app = FastAPI(
    title="AboutSamuel Agent Service",
    description="LangGraph-powered AI agent backend for AboutSamuel.com",
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs",
)

# CORS — only allow the C# API (internal Railway network) and local dev
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:*",
        "http://portfolioapi.railway.internal:*",
    ],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(health_router)
app.include_router(chat_router)
app.include_router(job_fit_router)
app.include_router(adversarial_router)
app.include_router(recruiters_router)
app.include_router(resume_analysis_router)
