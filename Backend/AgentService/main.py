import structlog
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from log_config import configure_logging, flush_logs

configure_logging()

from api.chat import router as chat_router
from api.health import router as health_router
from database.connection import close_pool
from messaging.connection import close_connection
from messaging.consumer import start_consumers

logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("agent_service_starting")
    try:
        await start_consumers()
        logger.info("agent_service_ready")
    except Exception as e:
        # Queue startup failure is non-fatal — HTTP endpoints still work
        logger.warning("queue_startup_failed", error=str(e))

    yield

    logger.info("agent_service_shutting_down")
    await close_pool()
    await close_connection()
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
