from fastapi import APIRouter
from database.connection import get_pool
from messaging.connection import get_channel

router = APIRouter(tags=["health"])


@router.get("/health")
async def health():
    checks = {"status": "healthy", "database": "unknown", "queue": "unknown"}

    try:
        pool = await get_pool()
        async with pool.acquire() as conn:
            await conn.fetchval("SELECT 1")
        checks["database"] = "healthy"
    except Exception as e:
        checks["database"] = f"unhealthy: {str(e)}"
        checks["status"] = "degraded"

    try:
        await get_channel()
        checks["queue"] = "healthy"
    except Exception as e:
        checks["queue"] = f"unhealthy: {str(e)}"
        checks["status"] = "degraded"

    return checks
