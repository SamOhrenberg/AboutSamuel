from langchain_core.tools import tool
from config import get_settings
import httpx
import structlog

logger = structlog.get_logger()


async def _call_csharp_api(path: str, body: dict) -> dict:
    settings = get_settings()
    async with httpx.AsyncClient(verify=False) as client:
        response = await client.post(
            f"{settings.csharp_api_url}{path}",
            json=body,
            headers={"X-Internal-Secret": settings.csharp_api_internal_secret},
            timeout=10.0,
        )
        response.raise_for_status()


@tool
async def contact_samuel(email: str, message: str = "") -> str:
    """
    Send a contact request to Samuel on behalf of the user.
    Use this when the user wants to get in touch with Samuel.
    Requires the user's email address.
    """
    try:
        await _call_csharp_api("/contact/internal", {"email": email, "message": message})
        return f"Contact request sent successfully from {email}."
    except Exception as e:
        logger.error("contact_samuel_failed", error=str(e), error_type=type(e).__name__, exc_info=True)
        return "Failed to send contact request. Please try the contact page directly."


@tool
def get_resume() -> str:
    """
    Give the user Samuel's official resume as a downloadable PDF card in the chat.
    Use this when the user asks to see, view, get, or download the resume.
    """
    # Sentinel for the stream handler, which turns it into meta.attachment for the frontend
    return "__ATTACHMENT__resume__"


# Must match the Vue file-based routes in Frontend/src/pages
VALID_PAGES = ["projects", "work-experience", "skill-map", "contact", "resume"]


@tool
def redirect_to_page(page: str) -> str:
    """
    Redirect the user to a specific page on the portfolio.
    Use this when the user asks about projects, work experience, or contact.
    Available pages: projects, work-experience, skill-map, contact, resume
    """
    page = page.strip().lower()
    if page not in VALID_PAGES:
        return f"Unknown page '{page}'. Valid options: {', '.join(VALID_PAGES)}"
    return f"__REDIRECT__{page}__"


@tool
def ask_clarification(question: str) -> str:
    """
    Ask the user a clarifying question when their request is ambiguous.
    Use this sparingly — only when genuinely needed to give a useful answer.
    """
    return f"__CLARIFY__{question}__"
