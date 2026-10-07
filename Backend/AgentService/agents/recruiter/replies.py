"""
Reply drafts, from templates rather than the LLM: predictable, in Samuel's voice, and
nothing in a recruiter's email can steer what they say. The only things taken from the
email are the recruiter's first name and the job title.
"""
import re
from typing import Literal

from agents.recruiter.decide import Decision
from agents.recruiter.state import RoleDetails

ReplyKind = Literal["decline", "ask_info", "interested", "represented"]

# One reason per decline, the most relevant first. A decline listing four problems reads
# like a lecture.
DECLINE_REASONS = [
    ("employment_type", "I'm only considering full-time or contract-to-hire roles right now, "
                        "so a straight contract isn't a fit for me."),
    ("contract_terms", "I'm only taking W2 roles, not C2C or 1099."),
    ("location", "I'm only considering remote roles or ones in the Oklahoma City area."),
    ("arrangement", "The work arrangement isn't one I'm considering right now."),
    ("pay", "The pay is below the range I'm looking for."),
    ("not_dev", "I'm focusing on hands-on software development and AI engineering roles."),
]

SIGN_OFF = "Thanks,\nSamuel Ohrenberg"

_COMPANYISH = re.compile(r"\b(inc|llc|corp|solutions|consulting|staffing|systems|group|technologies|technology|team|"
                         r"talent|recruiting|recruitment|services|hr|careers|jobs|infotech|info|tech|it|global|"
                         r"international|network|software|digital|labs|partners|resources|associates|recruiter|virtual|assistant|bot)\b", re.IGNORECASE)


def first_name(recruiter_name: str | None, from_name: str | None) -> str | None:
    """A first name to greet, or None when the sender looks like a company or a mailbox."""
    for name, source in ((recruiter_name, "extracted"), (from_name, "header")):
        if not name or _COMPANYISH.search(name):
            continue
        # A one-word display name ("noreply", "Recruiting") is a mailbox, not a person
        if source == "header" and len(name.split()) < 2:
            continue
        first = name.strip().split()[0].strip(",.")
        if first.isalpha() and len(first) > 1:
            return first.capitalize()
    return None


def _job(role: RoleDetails) -> str:
    title = role.job_title or "this"
    return f"the {title} role" if role.job_title else "this role"


def compose(kind: ReplyKind, role: RoleDetails, decision: Decision, recruiter_name: str | None,
            from_name: str | None) -> str:
    name = first_name(recruiter_name, from_name)
    greeting = f"Hi {name}," if name else "Hi,"
    job = _job(role)

    if kind == "decline":
        reason = next((text for code, text in DECLINE_REASONS if code in decision.codes),
                      "It isn't the right fit for what I'm looking for right now.")
        body = (f"Thanks for reaching out about {job}. {reason} "
                "I'd be glad to hear about other openings that fit, though.")
    elif kind == "ask_info":
        body = (f"Thanks for reaching out about {job}. Before I decide, could you share "
                f"{_join(decision.missing)}?")
    elif kind == "represented":
        body = (f"Thanks for reaching out about {job}. I'm already being represented for this position, "
                "so I'll have to pass on this one. I'd be glad to hear about other roles that fit.")
    else:  # interested: a starting point for Samuel to edit, never sent automatically
        ask = f" Could you share {_join(decision.missing)}, and" if decision.missing else " Could you share"
        body = (f"Thanks for reaching out about {job}. I'm interested and would like to learn more."
                f"{ask} a few times that work for a quick call?")

    return f"{greeting}\n\n{body}\n\n{SIGN_OFF}\n"


def _join(items: list[str]) -> str:
    if len(items) <= 1:
        return "".join(items)
    return ", ".join(items[:-1]) + f", and {items[-1]}"
