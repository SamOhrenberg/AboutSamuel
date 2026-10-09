"""
What triage does in Gmail for each new recruiter email: a label, usually a reply draft,
and a notification email for possible matches.

Safety rules, all enforced here:
  - Shadow mode only creates drafts. Live mode sends declines, questions, and "already
    represented" replies, up to a daily cap. Replies to possible matches are never sent.
  - Only emails received in the last RECENT_HOURS get a draft, so a backfill never
    answers month-old mail.
  - At most one reply per thread, and none if Samuel has already replied in it.
  - Replies go only to the original sender, in the same thread, from templates.
  - Platform notifications (LinkedIn and the like) and emails pitching several roles at
    once only get a label.
"""
import re
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Literal

import httpx
import structlog

from agents.recruiter import store
from agents.recruiter.agent import Pitch, Posting, _company_tokens
from agents.recruiter.decide import PLATFORM_DOMAINS
from agents.recruiter.replies import compose
from config import get_settings
from tools import gmail
from tools.resume import RESUME_FILENAME, get_resume_pdf

logger = structlog.get_logger()

Kind = Literal["conversation", "review", "ask_info", "represented", "decline"]

# When one email pitches roles that land differently, the most important one wins
PRIORITY: list[Kind] = ["conversation", "review", "ask_info", "represented", "decline"]

LABELS: dict[Kind, str] = {
    "conversation": "Recruiters/Conversation",
    "review": "Recruiters/Review",
    "ask_info": "Recruiters/Asked",
    "represented": "Recruiters/Represented",
    "decline": "Recruiters/Declined",
}
REPLY_KIND = {"review": "interested", "ask_info": "ask_info", "represented": "represented", "decline": "decline"}
AUTO_SEND: set[Kind] = {"ask_info", "represented", "decline"}
# The resume goes only with replies that say Samuel is interested, which are never auto-sent
ATTACH_RESUME: set[Kind] = {"review"}

# Mailboxes nobody reads: replying is pointless and looks automated
NO_REPLY = re.compile(r"^(no-?reply|do-?not-?reply|notifications?|mailer-daemon)\b", re.IGNORECASE)

RECENT_HOURS = 48
DAILY_SEND_CAP = 20


@dataclass
class Plan:
    pitch: Pitch
    posting: Posting
    kind: Kind
    draft: str | None          # the reply text, or None for label only
    no_draft_reason: str = ""

    @property
    def email(self):
        return self.pitch.email


def _kind(pitch: Pitch, posting: Posting) -> Kind:
    if pitch.category == "application_update":
        return "conversation"
    status = posting.stored.review_status if posting.stored else None
    if status == "passed":
        return "decline"
    d = posting.decision
    if d.engaged_with:
        sender = _company_tokens(pitch.role.recruiting_agency or pitch.email.from_address.split("@")[-1])
        if any(sender & _company_tokens(name) for name in d.engaged_with):
            return "conversation"  # a follow-up from the agency Samuel is already working with
        return "represented"
    if d.outcome == "review" or status == "interested":
        return "review"
    return d.outcome


def plan_actions(postings: list[Posting], received_at, acted_threads: set[str]) -> list[Plan]:
    """One plan per new email. received_at maps an EmailMessage to its UTC datetime."""
    by_email: dict[str, list[tuple[Kind, Pitch, Posting]]] = {}
    for posting in postings:
        for pitch in posting.new_pitches:
            by_email.setdefault(pitch.email.id, []).append((_kind(pitch, posting), pitch, posting))

    cutoff = datetime.now(timezone.utc) - timedelta(hours=RECENT_HOURS)
    threads = set(acted_threads)
    plans = []
    for candidates in by_email.values():
        kind, pitch, posting = min(candidates, key=lambda c: PRIORITY.index(c[0]))
        email = pitch.email
        reason = ""
        if kind == "conversation":
            reason = "already in conversation"
        elif email.from_address.endswith(PLATFORM_DOMAINS):
            reason = "platform notification, can't reply by email"
        elif NO_REPLY.match((email.reply_to or email.from_address).split("@")[0]):
            reason = "no-reply address"
        elif len(candidates) > 1 and kind != "review":
            reason = "pitched several roles"
        elif received_at(email) < cutoff:
            reason = f"older than {RECENT_HOURS} hours"
        elif email.thread_id in threads:
            reason = "already replied in this thread"

        draft = None
        if not reason:
            draft = compose(REPLY_KIND[kind], posting.role, posting.decision, pitch.role.recruiter_name,
                            email.from_name)
            threads.add(email.thread_id)
        plans.append(Plan(pitch, posting, kind, draft, reason))
    return plans


async def apply_actions(plans: list[Plan], shadow_mode: bool) -> dict:
    """Labels, drafts, sends (live mode only), and records what was done. Returns counts."""
    counts = {"labeled": 0, "drafted": 0, "sent": 0, "skipped_samuel_replied": 0}
    sent_today = await store.sent_today()
    resume = None  # fetched on the first reply that needs it
    async with httpx.AsyncClient(timeout=30) as client:
        for plan in plans:
            email = plan.email
            try:
                await gmail.add_label(client, email.id, await gmail.ensure_label(client, LABELS[plan.kind]))
                action, draft_id = "labeled", None

                if plan.draft and await gmail.samuel_replied_in_thread(client, email.thread_id):
                    counts["skipped_samuel_replied"] += 1
                elif plan.draft:
                    attachment = None
                    if plan.kind in ATTACH_RESUME:
                        resume = resume or await get_resume_pdf()
                        attachment = (RESUME_FILENAME, resume)
                    draft_id = await gmail.create_reply_draft(client, email, plan.draft, attachment)
                    action = "drafted"
                    if not shadow_mode and plan.kind in AUTO_SEND and sent_today < DAILY_SEND_CAP:
                        await gmail.send_draft(client, draft_id)
                        action, sent_today = "sent", sent_today + 1

                await store.set_action(email.id, action, draft_id)
                counts[action] += 1
            except Exception as e:
                # One bad email shouldn't stop the rest. It stays at action "none".
                logger.error("recruiter_action_failed", kind=plan.kind, error=str(e),
                             error_type=type(e).__name__)
    return counts


async def notify_matches(plans: list[Plan]) -> None:
    """One email to Samuel per run, listing postings that need his review."""
    postings: dict[int, Posting] = {}
    for plan in plans:
        if plan.kind == "review":
            postings[id(plan.posting)] = plan.posting
    if not postings:
        return

    items = []
    for posting in postings.values():
        r, d = posting.role, posting.decision
        items.append({
            "jobTitle": r.job_title, "company": r.hiring_company, "location": r.location,
            "workArrangement": r.work_arrangement, "annualPay": d.annual_pay,
            "agencies": posting.agencies[:10], "reasons": d.reasons[:5],
            "gmailUrl": f"https://mail.google.com/mail/u/0/#all/{posting.new_pitches[0].email.thread_id}",
        })

    settings = get_settings()
    try:
        async with httpx.AsyncClient(timeout=15) as client:
            resp = await client.post(f"{settings.csharp_api_url}/recruiters/internal/notify", json={"postings": items},
                                     headers={"X-Internal-Secret": settings.csharp_api_internal_secret})
            resp.raise_for_status()
    except Exception as e:
        logger.error("recruiter_notify_failed", error=str(e), error_type=type(e).__name__)


def describe(plans: list[Plan]) -> str:
    """Readable preview of what a run would do. For the CLI only: it includes email content."""
    lines = []
    for p in plans:
        e = p.email
        lines.append(f"[{p.kind}] {LABELS[p.kind]}  from {e.from_address}  \"{e.subject[:70]}\"")
        lines.append(f"    posting: {p.posting.role.job_title} | outcome {p.posting.decision.outcome}")
        if p.draft:
            lines += ["    draft:"] + [f"      {line}" for line in p.draft.splitlines()]
        else:
            lines.append(f"    no draft: {p.no_draft_reason}")
    return "\n".join(lines)
