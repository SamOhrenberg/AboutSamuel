"""
Reads and writes recruiter triage tables. The schema is owned by the C# API's EF migration
(AddRecruiterTriage), so column names here must match those models.

Privacy: subjects and snippets are only kept for recruiter outreach and application
updates. Other job-related mail that went through the classifier is stored as an id and
a category only, so it isn't classified twice. Full bodies are never stored.
"""
import json
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone

from agents.recruiter.decide import Decision, annualized_pay
from agents.recruiter.preferences import Preferences
from agents.recruiter.state import EmailTriage, RoleDetails
from database.connection import get_pool
from tools.gmail import EmailMessage

SNIPPET_CHARS = 300
KEEP_CONTENT_FOR = {"recruiter_outreach", "application_update"}


def _now() -> datetime:
    return datetime.now(timezone.utc)


# ---- settings -----------------------------------------------------------------

@dataclass
class TriageSettings:
    enabled: bool
    shadow_mode: bool
    last_synced_at: datetime | None
    preferences: Preferences


async def load_settings() -> TriageSettings:
    pool = await get_pool()
    async with pool.acquire() as conn:
        r = await conn.fetchrow('SELECT * FROM "RecruiterTriageSettings" WHERE "RecruiterTriageSettingsId" = 1')
    prefs = Preferences(
        remote_pay_floor=r["RemotePayFloor"], hybrid_pay_floor=r["HybridPayFloor"],
        onsite_pay_floor=r["OnsitePayFloor"], hours_per_year=r["HoursPerYear"],
        allow_remote=r["AllowRemote"], allow_hybrid=r["AllowHybrid"], allow_onsite=r["AllowOnsite"],
        acceptable_locations=json.loads(r["AcceptableLocations"]), home_state=r["HomeState"],
        allowed_employment_types=json.loads(r["AllowedEmploymentTypes"]),
        dealbreaker_contract_terms=json.loads(r["DealbreakerContractTerms"]),
        free_text_requirements=r["FreeTextRequirements"],
    )
    return TriageSettings(r["TriageEnabled"], r["ShadowMode"], r["LastSyncedAt"], prefs)


async def mark_synced(at: datetime) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute('UPDATE "RecruiterTriageSettings" SET "LastSyncedAt" = $1 '
                           'WHERE "RecruiterTriageSettingsId" = 1', at)


# ---- emails -------------------------------------------------------------------

async def already_processed(message_ids: list[str]) -> set[str]:
    if not message_ids:
        return set()
    pool = await get_pool()
    async with pool.acquire() as conn:
        rows = await conn.fetch('SELECT "GmailMessageId" FROM "RecruiterEmails" WHERE "GmailMessageId" = ANY($1)',
                                message_ids)
    return {r["GmailMessageId"] for r in rows}


async def save_email(conn, m: EmailMessage, t: EmailTriage, received_at: datetime) -> uuid.UUID | None:
    """Returns the new row's id, or None if this message was already saved (a concurrent run)."""
    keep = t.category in KEEP_CONTENT_FOR
    email_id = uuid.uuid4()
    inserted = await conn.fetchval(
        """
        INSERT INTO "RecruiterEmails"
            ("RecruiterEmailId", "GmailMessageId", "GmailThreadId", "ReceivedAt", "ProcessedAt",
             "FromName", "FromAddress", "Subject", "Snippet", "Category", "Personalized", "Confidence", "Action")
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, 'none')
        ON CONFLICT ("GmailMessageId") DO NOTHING
        RETURNING "RecruiterEmailId"
        """,
        email_id, m.id, m.thread_id, received_at, _now(),
        m.from_name if keep else None, m.from_address if keep else "",
        m.subject if keep else "", (" ".join(m.body.split())[:SNIPPET_CHARS]) if keep else "",
        t.category, t.personalized, t.confidence,
    )
    return inserted


# ---- postings -----------------------------------------------------------------

@dataclass
class StoredPitch:
    """A pitch already in the database, enough to re-merge and re-decide its posting."""
    role: RoleDetails
    from_address: str
    subject: str
    thread_id: str
    category: str


@dataclass
class StoredPosting:
    posting_id: uuid.UUID
    role: RoleDetails
    embedding: list[float] | None
    review_status: str
    pitches: list[StoredPitch]

    @property
    def thread_ids(self) -> set[str]:
        return {p.thread_id for p in self.pitches}


async def recent_postings(days: int = 60) -> list[StoredPosting]:
    """Postings seen recently, with their pitches, for matching new emails against."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        postings = await conn.fetch(
            """SELECT "RecruiterPostingId", "Role", "Embedding", "ReviewStatus" FROM "RecruiterPostings"
               WHERE "LastSeenAt" > now() - make_interval(days => $1)""", days)
        pitches = await conn.fetch(
            """SELECT p."RecruiterPostingId", p."Role", e."FromAddress", e."Subject", e."GmailThreadId", e."Category"
               FROM "RecruiterPitches" p JOIN "RecruiterEmails" e ON e."RecruiterEmailId" = p."RecruiterEmailId"
               WHERE p."RecruiterPostingId" = ANY($1)""", [r["RecruiterPostingId"] for r in postings])

    by_posting: dict[uuid.UUID, list[StoredPitch]] = {}
    for p in pitches:
        by_posting.setdefault(p["RecruiterPostingId"], []).append(StoredPitch(
            RoleDetails(**json.loads(p["Role"])), p["FromAddress"], p["Subject"], p["GmailThreadId"], p["Category"]))
    return [
        StoredPosting(r["RecruiterPostingId"], RoleDetails(**json.loads(r["Role"])),
                      list(r["Embedding"]) if r["Embedding"] is not None else None,
                      r["ReviewStatus"], by_posting.get(r["RecruiterPostingId"], []))
        for r in postings
    ]


def _review_status(current: str | None, outcome: str) -> str:
    """Samuel's own calls (interested, passed) are never overwritten by new emails."""
    if current in ("interested", "passed"):
        return current
    return "pending" if outcome == "review" else "auto"


async def upsert_posting(conn, posting_id: uuid.UUID | None, role: RoleDetails, embedding: list[float],
                         decision: Decision, conflicts: list[str], current_status: str | None,
                         seen_at: datetime) -> uuid.UUID:
    values = (
        role.job_title, role.hiring_company, role.work_arrangement, role.location, role.employment_type,
        role.contract_terms, decision.annual_pay, role.model_dump_json(), embedding, decision.outcome,
        json.dumps(decision.reasons), json.dumps(decision.missing), json.dumps(conflicts), decision.can_reply,
        _review_status(current_status, decision.outcome), seen_at,
    )
    if posting_id is None:
        posting_id = uuid.uuid4()
        await conn.execute(
            """
            INSERT INTO "RecruiterPostings"
                ("JobTitle", "HiringCompany", "WorkArrangement", "Location", "EmploymentType", "ContractTerms",
                 "AnnualPay", "Role", "Embedding", "Outcome", "Reasons", "Missing", "Conflicts", "CanReply",
                 "ReviewStatus", "LastSeenAt", "FirstSeenAt", "RecruiterPostingId")
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $16, $17)
            """, *values, posting_id)
    else:
        await conn.execute(
            """
            UPDATE "RecruiterPostings" SET
                "JobTitle" = $1, "HiringCompany" = $2, "WorkArrangement" = $3, "Location" = $4,
                "EmploymentType" = $5, "ContractTerms" = $6, "AnnualPay" = $7, "Role" = $8, "Embedding" = $9,
                "Outcome" = $10, "Reasons" = $11, "Missing" = $12, "Conflicts" = $13, "CanReply" = $14,
                "ReviewStatus" = $15, "LastSeenAt" = GREATEST("LastSeenAt", $16)
            WHERE "RecruiterPostingId" = $17
            """, *values, posting_id)
    return posting_id


async def add_pitch(conn, email_id: uuid.UUID, posting_id: uuid.UUID, role: RoleDetails,
                    from_address: str, prefs: Preferences) -> None:
    await conn.execute(
        """
        INSERT INTO "RecruiterPitches"
            ("RecruiterPitchId", "RecruiterEmailId", "RecruiterPostingId", "RecruitingAgency", "RecruiterName",
             "AnnualPay", "Role")
        VALUES ($1, $2, $3, $4, $5, $6, $7)
        """,
        uuid.uuid4(), email_id, posting_id, role.recruiting_agency or from_address.split("@")[-1],
        role.recruiter_name, annualized_pay(role, prefs), role.model_dump_json(),
    )


# ---- actions ------------------------------------------------------------------

async def threads_with_reply(thread_ids: list[str]) -> set[str]:
    """Threads triage has already drafted or sent a reply in."""
    if not thread_ids:
        return set()
    pool = await get_pool()
    async with pool.acquire() as conn:
        rows = await conn.fetch(
            """SELECT DISTINCT "GmailThreadId" FROM "RecruiterEmails"
               WHERE "GmailThreadId" = ANY($1) AND "Action" IN ('drafted', 'sent')""", thread_ids)
    return {r["GmailThreadId"] for r in rows}


async def sent_today() -> int:
    pool = await get_pool()
    async with pool.acquire() as conn:
        return await conn.fetchval(
            """SELECT count(*) FROM "RecruiterEmails"
               WHERE "Action" = 'sent' AND "ProcessedAt" > now() - interval '1 day'""")


async def set_action(gmail_message_id: str, action: str, draft_id: str | None) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            """UPDATE "RecruiterEmails" SET "Action" = $2, "GmailDraftId" = $3 WHERE "GmailMessageId" = $1""",
            gmail_message_id, action, draft_id)
