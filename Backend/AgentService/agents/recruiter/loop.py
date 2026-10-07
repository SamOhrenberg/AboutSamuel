"""
Runs recruiter triage in the background. The admin page's "enabled" switch is read from
the database every cycle, so turning triage on or off needs no redeploy.
"""
import asyncio

import structlog

from agents.recruiter import store
from agents.recruiter.pipeline import process_new_mail
from tools.gmail import gmail_configured

logger = structlog.get_logger()

INTERVAL_SECONDS = 15 * 60
LOOKBACK_DAYS = 2  # overlaps the interval generously; processed emails are skipped
BACKFILL_DAYS = 30
BACKFILL_LIMIT = 2000  # a month of inbox can pass 500; only job-related mail reaches the LLM


async def run_forever() -> None:
    if not gmail_configured():
        logger.info("recruiter_triage_disabled", reason="GMAIL_* settings are not set")
        return
    while True:
        try:
            settings = await store.load_settings()
            if settings.enabled:
                if settings.last_synced_at is None:
                    # First run ever: learn the last month as history, without touching
                    # Gmail. The newest days are left for the normal run right after, so
                    # they still get labels and drafts.
                    logger.info("recruiter_triage_backfill_starting", days=BACKFILL_DAYS)
                    await process_new_mail(BACKFILL_DAYS, BACKFILL_LIMIT, mode="off", skip_recent_days=LOOKBACK_DAYS)
                await process_new_mail(LOOKBACK_DAYS, mode="apply")
        except asyncio.CancelledError:
            raise
        except Exception as e:
            logger.error("recruiter_triage_failed", error=str(e), error_type=type(e).__name__)
        await asyncio.sleep(INTERVAL_SECONDS)
