import json
import time

from langchain_core.tools import tool
from langchain_openai import AzureOpenAIEmbeddings
from database.connection import get_pool
from config import get_settings
import structlog

logger = structlog.get_logger()


def _get_embeddings() -> AzureOpenAIEmbeddings:
    settings = get_settings()
    return AzureOpenAIEmbeddings(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=settings.azure_openai_embedding_deployment,
        api_version=settings.azure_openai_api_version,
    )


async def embed_query(text: str) -> list[float]:
    embeddings = _get_embeddings()
    return await embeddings.aembed_query(text)


async def embed_queries(texts: list[str]) -> list[list[float]]:
    """Embed several texts in one API call instead of one call each."""
    embeddings = _get_embeddings()
    return await embeddings.aembed_documents(texts)


async def search_pgvector(query_embedding: list[float], limit: int = 8) -> list[dict]:
    """
    Query all three entity tables using pgvector cosine distance,
    merge results, and return the top entries by similarity score.
    """
    pool = await get_pool()

    async with pool.acquire() as conn:
        info_rows = await conn.fetch(
            """
            SELECT "InformationId"::text AS id,
                   "Text"               AS content,
                   'information'        AS entity_type,
                   NULL                 AS sub_label,
                   '[]'::text           AS tech_stack,
                   1 - ("Embedding" <=> $1) AS score
            FROM "Information"
            WHERE "Embedding" IS NOT NULL
            ORDER BY "Embedding" <=> $1
            LIMIT $2
            """,
            query_embedding, limit,
        )

        project_rows = await conn.fetch(
            """
            SELECT p."ProjectId"::text AS id,
                   (
                       'Project: ' || p."Title" || E'\n' ||
                       'Role: '    || p."Role"  || E'\n' ||
                       COALESCE('Years: ' || p."StartYear" || ' - ' || COALESCE(p."EndYear", 'present') || E'\n', '') ||
                       COALESCE('Summary: ' || p."Summary", '')
                   )                   AS content,
                   'project'           AS entity_type,
                   p."Title"           AS sub_label,
                   p."TechStack"       AS tech_stack,
                   1 - (p."Embedding" <=> $1) AS score
            FROM "Projects" p
            WHERE p."IsActive" = true AND p."Embedding" IS NOT NULL
            ORDER BY p."Embedding" <=> $1
            LIMIT $2
            """,
            query_embedding, limit,
        )

        work_rows = await conn.fetch(
            """
            SELECT "WorkExperienceId"::text AS id,
                   (
                       'Role: '     || "Title"    || E'\n' ||
                       'Employer: ' || "Employer" || E'\n' ||
                       COALESCE('Years: ' || "StartYear" || ' - ' || COALESCE("EndYear", 'present') || E'\n', '') ||
                       COALESCE('Summary: ' || "Summary", '')
                   )                         AS content,
                   'work'                    AS entity_type,
                   "Employer"                AS sub_label,
                   '[]'::text                AS tech_stack,
                   1 - ("Embedding" <=> $1) AS score
            FROM "WorkExperiences"
            WHERE "IsActive" = true AND "Embedding" IS NOT NULL
            ORDER BY "Embedding" <=> $1
            LIMIT $2
            """,
            query_embedding, limit,
        )

    # Merge and re-rank across all three tables
    all_rows = list(info_rows) + list(project_rows) + list(work_rows)
    sorted_rows = sorted(all_rows, key=lambda r: r["score"], reverse=True)
    return [dict(r) for r in sorted_rows[:limit]]


def with_tech_stack(content: str, tech_stack: str | None) -> str:
    """Projects keep their technologies in a separate JSON column. Without it the
    LLM can't tell that, say, a project used Python."""
    try:
        techs = json.loads(tech_stack or "[]")
    except json.JSONDecodeError:
        techs = []
    return f"{content}\nTech stack: {', '.join(techs)}" if techs else content


async def get_career_timeline() -> list[dict]:
    """Every active role with its years, oldest first. Ground truth for questions about
    total experience, which similarity search can't answer (it only returns a few hits)."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        rows = await conn.fetch(
            """
            SELECT "Employer" AS employer, "Title" AS title,
                   "StartYear" AS start_year, "EndYear" AS end_year
            FROM "WorkExperiences"
            WHERE "IsActive" = true
            ORDER BY "StartYear" NULLS LAST, "DisplayOrder"
            """
        )
    return [dict(r) for r in rows]


async def get_project_catalog() -> list[dict]:
    """Every active project with its role, years, and tech stack (as a list)."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        rows = await conn.fetch(
            """
            SELECT "Title" AS title, "Role" AS role, "TechStack" AS tech_stack,
                   "StartYear" AS start_year, "EndYear" AS end_year
            FROM "Projects"
            WHERE "IsActive" = true
            ORDER BY "DisplayOrder"
            """
        )
    catalog = []
    for r in rows:
        try:
            techs = json.loads(r["tech_stack"] or "[]")
        except json.JSONDecodeError:
            techs = []
        catalog.append({**dict(r), "tech_stack": techs})
    return catalog


def format_portfolio_overview(timeline: list[dict], catalog: list[dict]) -> str:
    """Timeline plus project catalog. SamuelLM gets it as context, the adversarial
    generator builds attacks from it, and the adversarial judge uses it as ground truth."""
    lines = ["CAREER TIMELINE"]
    lines += [f"- {r['title']} at {r['employer']}: {r['start_year'] or '?'} - {r['end_year'] or 'present'}"
              for r in timeline]
    lines += ["", "PROJECTS"]
    lines += [f"- {p['title']} ({p['role']}, {p['start_year'] or '?'} - {p['end_year'] or 'present'}): "
              f"{', '.join(p['tech_stack']) or 'no tech stack listed'}"
              for p in catalog]
    return "\n".join(lines)


PORTFOLIO_OVERVIEW_TTL_SECONDS = 600
_overview_cache: tuple[float, str] | None = None


async def get_portfolio_overview_cached() -> str:
    """The overview, cached so chat doesn't query Postgres on every message. Admin edits
    show up within the TTL. If the database is unreachable, returns the last good copy
    (or "" if there never was one) so chat keeps working without it."""
    global _overview_cache
    now = time.monotonic()
    if _overview_cache and now - _overview_cache[0] < PORTFOLIO_OVERVIEW_TTL_SECONDS:
        return _overview_cache[1]
    try:
        overview = format_portfolio_overview(await get_career_timeline(), await get_project_catalog())
        _overview_cache = (now, overview)
        return overview
    except Exception as e:
        logger.warning("portfolio_overview_unavailable", error=str(e), error_type=type(e).__name__)
        return _overview_cache[1] if _overview_cache else ""


@tool
async def search_experience(query: str) -> str:
    """
    Search Samuel's experience, projects, skills, and background
    using semantic similarity. Use this whenever the user asks anything
    about Samuel's work, skills, projects, or background.
    Returns relevant context passages.
    """
    try:
        embedding = await embed_query(query)
        results = await search_pgvector(embedding, limit=8)

        if not results:
            return "No relevant experience found for this query."

        passages = []
        for r in results:
            passages.append(r["content"])

        return "\n\n---\n\n".join(passages)

    except Exception as e:
        logger.error("search_experience_failed", error=str(e))
        return "Search temporarily unavailable."
