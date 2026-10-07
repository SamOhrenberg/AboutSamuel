"""
The recruiter triage run: fetch new inbox mail, classify it, match pitches against postings
already on file, decide, and save. Used for the one-time backfill and, later, the
background loop.

Modes:
  off      classify and save only, nothing in Gmail (the backfill)
  preview  everything, then print the planned actions and roll the database back
  apply    save, then label, draft, and notify (see actions.py for the safety rules)
"""
import asyncio
from datetime import datetime, timezone
from typing import Literal
from email.utils import parsedate_to_datetime

import httpx
import structlog

from agents.recruiter import actions, store
from agents.recruiter.agent import (
    PITCH_CATEGORIES, Pitch, decide_posting, group_into_postings, looks_job_related, role_fingerprint_text, triage_email,
)
from database.connection import get_pool
from tools.gmail import get_message, get_message_summary, list_message_ids
from tools.llm_retry import with_rate_limit_retry
from tools.retrieval import embed_queries

logger = structlog.get_logger()

# Any constant works; it just has to be the same for every run
TRIAGE_LOCK_KEY = 4_172_026
MATCH_AGAINST_DAYS = 60


Mode = Literal["off", "preview", "apply"]


class _Rollback(Exception):
    pass


def _received_at(date_header: str) -> datetime:
    try:
        return parsedate_to_datetime(date_header).astimezone(timezone.utc)
    except (TypeError, ValueError, IndexError):
        return datetime.now(timezone.utc)


async def process_new_mail(lookback_days: int, limit: int = 500, mode: Mode = "off", skip_recent_days: int = 0) -> dict:
    """Returns counts for logging. Safe to run repeatedly: processed messages are skipped.
    skip_recent_days leaves the newest mail for a later run (the first backfill uses it)."""
    pool = await get_pool()
    async with pool.acquire() as lock_conn:
        if not await lock_conn.fetchval("SELECT pg_try_advisory_lock($1)", TRIAGE_LOCK_KEY):
            logger.info("recruiter_triage_skipped", reason="another run is in progress")
            return {"skipped": True}
        try:
            return await _run(lookback_days, limit, mode, skip_recent_days)
        finally:
            await lock_conn.execute("SELECT pg_advisory_unlock($1)", TRIAGE_LOCK_KEY)


async def _run(lookback_days: int, limit: int, mode: Mode, skip_recent_days: int) -> dict:
    started = datetime.now(timezone.utc)
    settings = await store.load_settings()
    prefs = settings.preferences

    async with httpx.AsyncClient(timeout=30) as client:
        query = f"in:inbox -from:me newer_than:{lookback_days}d"
        if skip_recent_days:
            query += f" older_than:{skip_recent_days}d"
        ids = await list_message_ids(client, query, limit)
        done = await store.already_processed(ids)
        new_ids = [i for i in ids if i not in done]

        sem = asyncio.Semaphore(4)  # Gmail's per-user rate limit trips around 8

        async def summary(i):
            async with sem:
                return await get_message_summary(client, i)

        summaries = await asyncio.gather(*(summary(i) for i in new_ids))
        candidates = [s for s in summaries if looks_job_related(s)]

        llm_sem = asyncio.Semaphore(2)  # shares the gpt-4.1-mini quota with the live chat

        async def classify(s):
            async with llm_sem:
                message = await get_message(client, s.id)
                return message, await triage_email(message)

        classified = await asyncio.gather(*(classify(s) for s in candidates))

    pitches = [Pitch(m, t, r) for m, t in classified if t.category in PITCH_CATEGORIES for r in t.roles]
    existing = await store.recent_postings(MATCH_AGAINST_DAYS)
    postings = await group_into_postings(pitches, prefs, existing)

    decide_sem = asyncio.Semaphore(2)

    async def decide_one(p):
        async with decide_sem:
            p.decision = await decide_posting(p, prefs)

    await asyncio.gather(*(decide_one(p) for p in postings))

    # One embedding per posting, of its merged details, for matching future emails
    posting_vectors = await with_rate_limit_retry(
        lambda: embed_queries([role_fingerprint_text(p.role) for p in postings]), "triage:embed_postings"
    ) if postings else []

    plans = []
    if mode != "off":
        thread_ids = list({p.email.thread_id for p in pitches})
        plans = actions.plan_actions(postings, lambda m: _received_at(m.date),
                                     await store.threads_with_reply(thread_ids))

    pool = await get_pool()
    try:
        await _persist(pool, classified, postings, posting_vectors, prefs, rollback=(mode == "preview"))
    except _Rollback:
        print(actions.describe(plans))
        return {"preview": True, "planned": len(plans)}

    await store.mark_synced(started)
    action_counts = {}
    if mode == "apply" and plans:
        action_counts = await actions.apply_actions(plans, settings.shadow_mode)
        await actions.notify_matches(plans)

    counts = {
        "inbox": len(ids), "new": len(new_ids), "classified": len(classified),
        "recruiter_emails": sum(1 for _, t in classified if t.category == "recruiter_outreach"),
        "postings_touched": len(postings),
        "postings_new": sum(1 for p in postings if p.stored is None),
        "postings_updated": sum(1 for p in postings if p.stored is not None),
        **action_counts,
    }
    logger.info("recruiter_triage_run", **counts)
    return counts


async def _persist(pool, classified, postings, posting_vectors, prefs, rollback: bool) -> None:
    """Everything in one transaction, so a failed run leaves nothing half saved."""
    async with pool.acquire() as conn:
        async with conn.transaction():
            email_ids = {}
            for m, t in classified:
                email_ids[m.id] = await store.save_email(conn, m, t, _received_at(m.date))

            for posting, vector in zip(postings, posting_vectors):
                saved_pitches = [p for p in posting.new_pitches if email_ids.get(p.email.id)]
                if not saved_pitches:
                    continue  # every email for it was saved by a concurrent run
                seen_at = max(_received_at(p.email.date) for p in saved_pitches)
                posting_id = await store.upsert_posting(
                    conn, posting.stored.posting_id if posting.stored else None, posting.role, vector,
                    posting.decision, posting.conflicts,
                    posting.stored.review_status if posting.stored else None, seen_at)
                for p in saved_pitches:
                    await store.add_pitch(conn, email_ids[p.email.id], posting_id, p.role, p.email.from_address, prefs)
            if rollback:
                raise _Rollback()


if __name__ == "__main__":
    #   python -m agents.recruiter.pipeline [lookback days] [off|preview|apply]
    import sys

    from database.connection import close_pool

    async def _main():
        try:
            days = int(sys.argv[1]) if len(sys.argv) > 1 else 2
            print(await process_new_mail(days, mode=sys.argv[2] if len(sys.argv) > 2 else "off"))
        finally:
            await close_pool()

    asyncio.run(_main())
