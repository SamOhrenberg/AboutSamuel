"""
Samuel marking postings as the same job. Triage never merges two stored postings on its
own (a wrong automatic merge is hard to spot and undo), so this is the manual version:
every pitch moves to one posting, which is then re-merged and re-decided exactly like an
automatic duplicate.
"""
import uuid

import structlog

from agents.recruiter import store
from agents.recruiter.agent import Posting, _merge, decide_posting, role_fingerprint_text, stored_pitch
from agents.recruiter.pipeline import TRIAGE_LOCK_KEY
from database.connection import get_pool
from tools.llm_retry import with_rate_limit_retry
from tools.retrieval import embed_queries

logger = structlog.get_logger()


class TriageBusy(Exception):
    """A triage run holds the lock; merging now could race it."""


def _combined_status(statuses: list[str]) -> str | None:
    """Samuel's own calls survive a merge. Interested beats passed, so an interest isn't lost."""
    for status in ("interested", "passed"):
        if status in statuses:
            return status
    return None  # let the new decision set pending or auto


async def merge_postings(target_id: uuid.UUID, source_ids: list[uuid.UUID]) -> None:
    source_ids = [s for s in dict.fromkeys(source_ids) if s != target_id]
    if not source_ids:
        raise ValueError("Pick at least two different postings.")

    pool = await get_pool()
    async with pool.acquire() as lock_conn:
        if not await lock_conn.fetchval("SELECT pg_try_advisory_lock($1)", TRIAGE_LOCK_KEY):
            raise TriageBusy()
        try:
            await _merge_locked(pool, target_id, source_ids)
        finally:
            await lock_conn.execute("SELECT pg_advisory_unlock($1)", TRIAGE_LOCK_KEY)


async def _merge_locked(pool, target_id: uuid.UUID, source_ids: list[uuid.UUID]) -> None:
    postings = {p.posting_id: p for p in await store.postings_by_id([target_id, *source_ids])}
    missing = [i for i in [target_id, *source_ids] if i not in postings]
    if missing:
        raise LookupError(f"No posting with id {missing[0]}")

    prefs = (await store.load_settings()).preferences
    pitches = [stored_pitch(sp) for p in postings.values() for sp in p.pitches]
    role, conflicts = _merge(pitches, prefs)
    posting = Posting(pitches=pitches, role=role, conflicts=conflicts)
    decision = await decide_posting(posting, prefs)
    [vector] = await with_rate_limit_retry(lambda: embed_queries([role_fingerprint_text(role)]), "triage:embed_merge")
    status = _combined_status([p.review_status for p in postings.values()])

    async with pool.acquire() as conn:
        async with conn.transaction():
            rows = await conn.fetch(
                """SELECT "RecruiterPostingId", "Notes", "FirstSeenAt", "LastSeenAt" FROM "RecruiterPostings"
                   WHERE "RecruiterPostingId" = ANY($1)""", [target_id, *source_ids])
            by_id = {r["RecruiterPostingId"]: r for r in rows}
            ordered = [by_id[target_id], *(by_id[s] for s in source_ids)]
            notes = "\n\n".join(r["Notes"] for r in ordered if r["Notes"]) or None

            await conn.execute(
                """UPDATE "RecruiterPitches" SET "RecruiterPostingId" = $1 WHERE "RecruiterPostingId" = ANY($2)""",
                target_id, source_ids)
            await conn.execute("""DELETE FROM "RecruiterPostings" WHERE "RecruiterPostingId" = ANY($1)""", source_ids)
            await store.upsert_posting(conn, target_id, role, vector, decision, conflicts, status,
                                       max(r["LastSeenAt"] for r in rows))
            await conn.execute(
                """UPDATE "RecruiterPostings" SET "Notes" = $2, "FirstSeenAt" = $3,
                       "ReviewedAt" = CASE WHEN $4 THEN COALESCE("ReviewedAt", now()) ELSE "ReviewedAt" END
                   WHERE "RecruiterPostingId" = $1""",
                target_id, notes, min(r["FirstSeenAt"] for r in rows), status is not None)

    logger.info("recruiter_postings_merged", merged=len(source_ids) + 1, outcome=decision.outcome)
