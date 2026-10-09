"""
Minimal async Gmail API client for recruiter triage.

Plain REST over httpx (the official Google client is synchronous). Authenticates with
the refresh token from scripts/gmail_auth.py, exchanging it for short-lived access
tokens as needed. Read-only for now: list and fetch messages. Labels, drafts, and
sending get added when triage is allowed to act.
"""
import asyncio
import base64
import re
import time
from dataclasses import dataclass, field
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import parseaddr
from html.parser import HTMLParser

import httpx

from config import get_settings

TOKEN_URL = "https://oauth2.googleapis.com/token"
API = "https://gmail.googleapis.com/gmail/v1/users/me"

_access_token: tuple[str, float] | None = None  # (token, monotonic time it expires)


class GmailNotConfigured(RuntimeError):
    pass


def gmail_configured() -> bool:
    s = get_settings()
    return bool(s.gmail_client_id and s.gmail_client_secret and s.gmail_refresh_token)


async def _token(client: httpx.AsyncClient) -> str:
    global _access_token
    if _access_token and time.monotonic() < _access_token[1]:
        return _access_token[0]
    if not gmail_configured():
        raise GmailNotConfigured("Set GMAIL_CLIENT_ID, GMAIL_CLIENT_SECRET, and GMAIL_REFRESH_TOKEN.")
    s = get_settings()
    resp = await client.post(TOKEN_URL, data={
        "client_id": s.gmail_client_id,
        "client_secret": s.gmail_client_secret,
        "refresh_token": s.gmail_refresh_token,
        "grant_type": "refresh_token",
    })
    resp.raise_for_status()
    body = resp.json()
    # Refresh a minute early so a token never expires mid-request
    _access_token = (body["access_token"], time.monotonic() + body["expires_in"] - 60)
    return _access_token[0]


RATE_LIMIT_REASONS = {"rateLimitExceeded", "userRateLimitExceeded"}
# Gmail's limit is a per-user budget of quota units per minute, so backing off has to
# be able to wait out the rest of the minute: 2 + 4 + 8 + 16 + 32 = 62 seconds.
MAX_ATTEMPTS = 6


def _is_rate_limited(resp: httpx.Response) -> bool:
    """Gmail signals its per-user rate limit with 429, or 403 plus a rateLimitExceeded reason."""
    if resp.status_code == 429:
        return True
    if resp.status_code != 403:
        return False
    try:
        errors = resp.json().get("error", {}).get("errors") or []
    except ValueError:
        return False
    return any(e.get("reason") in RATE_LIMIT_REASONS for e in errors)


async def _request(client: httpx.AsyncClient, method: str, path: str, params: dict | None = None,
                   body: dict | None = None) -> dict:
    for attempt in range(1, MAX_ATTEMPTS + 1):
        resp = await client.request(method, f"{API}{path}", params=params, json=body,
                                    headers={"Authorization": f"Bearer {await _token(client)}"})
        if _is_rate_limited(resp) and attempt < MAX_ATTEMPTS:
            await asyncio.sleep(2 ** attempt)
            continue
        resp.raise_for_status()
        return resp.json() if resp.content else {}
    raise RuntimeError("unreachable")


async def _get(client: httpx.AsyncClient, path: str, params: dict | None = None) -> dict:
    return await _request(client, "GET", path, params)


@dataclass
class EmailMessage:
    id: str
    thread_id: str
    from_name: str
    from_address: str
    reply_to: str | None
    subject: str
    date: str
    body: str
    label_ids: list[str] = field(default_factory=list)
    # For threading a reply: the original's Message-ID and References headers
    message_id_header: str = ""
    references: str = ""


async def list_message_ids(client: httpx.AsyncClient, query: str, limit: int) -> list[str]:
    """Message ids matching a Gmail search query, newest first."""
    ids: list[str] = []
    page_token = None
    while len(ids) < limit:
        params = {"q": query, "maxResults": min(100, limit - len(ids))}
        if page_token:
            params["pageToken"] = page_token
        data = await _get(client, "/messages", params)
        ids += [m["id"] for m in data.get("messages", [])]
        page_token = data.get("nextPageToken")
        if not page_token:
            break
    return ids[:limit]


@dataclass
class MessageSummary:
    """Headers, labels, and Gmail's snippet: enough to pre-filter without the body."""
    id: str
    thread_id: str
    from_address: str
    subject: str
    snippet: str
    label_ids: list[str]
    is_bulk: bool  # has a List-Unsubscribe header (mass email tools, newsletters)


async def get_message_summary(client: httpx.AsyncClient, message_id: str) -> MessageSummary:
    data = await _get(client, f"/messages/{message_id}", {
        "format": "metadata", "metadataHeaders": ["From", "Subject", "List-Unsubscribe"]})
    headers = {h["name"].lower(): h["value"] for h in data["payload"].get("headers", [])}
    return MessageSummary(
        id=data["id"],
        thread_id=data["threadId"],
        from_address=parseaddr(headers.get("from", ""))[1].lower(),
        subject=headers.get("subject", ""),
        snippet=data.get("snippet", ""),
        label_ids=data.get("labelIds", []),
        is_bulk="list-unsubscribe" in headers,
    )


async def get_message(client: httpx.AsyncClient, message_id: str) -> EmailMessage:
    data = await _get(client, f"/messages/{message_id}", {"format": "full"})
    headers = {h["name"].lower(): h["value"] for h in data["payload"].get("headers", [])}
    from_name, from_address = parseaddr(headers.get("from", ""))
    return EmailMessage(
        id=data["id"],
        thread_id=data["threadId"],
        from_name=from_name,
        from_address=from_address.lower(),
        reply_to=parseaddr(headers["reply-to"])[1].lower() if "reply-to" in headers else None,
        message_id_header=headers.get("message-id", ""),
        references=headers.get("references", ""),
        subject=headers.get("subject", ""),
        date=headers.get("date", ""),
        body=_body_text(data["payload"]),
        label_ids=data.get("labelIds", []),
    )


# ---- body extraction ------------------------------------------------------

def _decode(data: str) -> str:
    return base64.urlsafe_b64decode(data + "=" * (-len(data) % 4)).decode("utf-8", errors="replace")


def _find_parts(payload: dict, mime_type: str) -> list[str]:
    found = []
    if payload.get("mimeType") == mime_type and payload.get("body", {}).get("data"):
        found.append(_decode(payload["body"]["data"]))
    for part in payload.get("parts", []) or []:
        found += _find_parts(part, mime_type)
    return found


class _TextFromHtml(HTMLParser):
    def __init__(self):
        super().__init__()
        self.chunks: list[str] = []
        self._skip = 0

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style"):
            self._skip += 1
        elif tag in ("br", "p", "div", "li", "tr", "h1", "h2", "h3"):
            self.chunks.append("\n")

    def handle_endtag(self, tag):
        if tag in ("script", "style") and self._skip:
            self._skip -= 1

    def handle_data(self, data):
        if not self._skip:
            self.chunks.append(data)


def _body_text(payload: dict) -> str:
    """Plain-text body, falling back to HTML stripped of tags."""
    plain = _find_parts(payload, "text/plain")
    if plain:
        text = "\n".join(plain)
    else:
        parser = _TextFromHtml()
        parser.feed("\n".join(_find_parts(payload, "text/html")))
        text = "".join(parser.chunks)
    return re.sub(r"\n\s*\n+", "\n\n", text).strip()


# ---- writing: labels and drafts ---------------------------------------------------
# Only recruiter triage uses these, and only through agents/recruiter/actions.py, which
# enforces the safety rules (shadow mode, one reply per thread, never if Samuel replied).

_label_ids: dict[str, str] = {}


async def ensure_label(client: httpx.AsyncClient, name: str) -> str:
    """The label's id, creating it the first time. Nested labels use "/" ("Recruiters/Review")."""
    if name in _label_ids:
        return _label_ids[name]
    for label in (await _get(client, "/labels")).get("labels", []):
        _label_ids[label["name"]] = label["id"]
    if name not in _label_ids:
        created = await _request(client, "POST", "/labels", body={
            "name": name, "labelListVisibility": "labelShow", "messageListVisibility": "show"})
        _label_ids[name] = created["id"]
    return _label_ids[name]


async def add_label(client: httpx.AsyncClient, message_id: str, label_id: str) -> None:
    await _request(client, "POST", f"/messages/{message_id}/modify", body={"addLabelIds": [label_id]})


async def samuel_replied_in_thread(client: httpx.AsyncClient, thread_id: str) -> bool:
    """True if any message in the thread was sent from this account."""
    thread = await _get(client, f"/threads/{thread_id}", {"format": "minimal"})
    return any("SENT" in m.get("labelIds", []) for m in thread.get("messages", []))


def _reply_mime(original: EmailMessage, body: str, attachment: tuple[str, bytes] | None = None) -> str:
    """A plain-text reply in the same conversation, base64url-encoded for the Gmail API.
    attachment is (filename, PDF bytes)."""
    text = MIMEText(body, "plain", "utf-8")
    if attachment:
        filename, pdf = attachment
        msg = MIMEMultipart()
        msg.attach(text)
        part = MIMEApplication(pdf, "pdf")
        part.add_header("Content-Disposition", "attachment", filename=filename)
        msg.attach(part)
    else:
        msg = text
    msg["To"] = original.reply_to or original.from_address
    subject = original.subject or ""
    msg["Subject"] = subject if subject.lower().startswith("re:") else f"Re: {subject}"
    if original.message_id_header:
        msg["In-Reply-To"] = original.message_id_header
        msg["References"] = f"{original.references} {original.message_id_header}".strip()
    return base64.urlsafe_b64encode(msg.as_bytes()).decode()


async def create_reply_draft(client: httpx.AsyncClient, original: EmailMessage, body: str,
                             attachment: tuple[str, bytes] | None = None) -> str:
    """Creates a draft reply in the original's thread. Returns the draft id. Sends nothing."""
    draft = await _request(client, "POST", "/drafts", body={
        "message": {"raw": _reply_mime(original, body, attachment), "threadId": original.thread_id}})
    return draft["id"]


async def send_draft(client: httpx.AsyncClient, draft_id: str) -> None:
    await _request(client, "POST", "/drafts/send", body={"id": draft_id})


async def delete_draft(client: httpx.AsyncClient, draft_id: str) -> None:
    await _request(client, "DELETE", f"/drafts/{draft_id}")
