"""
The official resume PDF. The admin panel uploads versions into the ResumeFiles table (hosts
have ephemeral disks, so the database is the store). assets/resume.pdf ships with the service
as the seed for an empty table and the fallback if the database can't be reached.
"""
import hashlib
from pathlib import Path

import structlog

from database.connection import get_pool

logger = structlog.get_logger()

BUNDLED_PATH = Path(__file__).resolve().parent.parent / "assets" / "resume.pdf"
RESUME_FILENAME = "Samuel_Ohrenberg_Resume.pdf"


def _bundled_bytes() -> bytes:
    return BUNDLED_PATH.read_bytes()


async def ensure_seeded() -> bool:
    """Inserts the bundled PDF as the current resume if the table is empty. True if it did."""
    content = _bundled_bytes()
    pool = await get_pool()
    async with pool.acquire() as conn:
        # ON CONFLICT covers two instances starting together: the partial unique index on
        # IsCurrent lets only one of them win
        result = await conn.execute(
            """INSERT INTO "ResumeFiles"
                   ("ResumeFileId", "FileName", "Content", "SizeBytes", "Sha256", "IsCurrent", "UploadedAt")
               SELECT gen_random_uuid(), $1, $2, $3, $4, true, now()
               WHERE NOT EXISTS (SELECT 1 FROM "ResumeFiles")
               ON CONFLICT DO NOTHING""",
            RESUME_FILENAME, content, len(content), hashlib.sha256(content).hexdigest())
    return result == "INSERT 0 1"


async def get_resume_pdf() -> bytes:
    """The current official resume. Falls back to the bundled copy rather than failing."""
    try:
        pool = await get_pool()
        async with pool.acquire() as conn:
            content = await conn.fetchval('SELECT "Content" FROM "ResumeFiles" WHERE "IsCurrent"')
        if content:
            return bytes(content)
    except Exception as e:
        logger.warning("resume_db_read_failed", error=str(e), error_type=type(e).__name__)
    return _bundled_bytes()
