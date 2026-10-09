"""
Database access for resume analysis. Tables are created by the C# EF migrations; this
module only reads the site data and writes analyses and their suggestions.
"""
import json
import uuid

import structlog

from agents.resume_analysis.state import SiteData, Suggestion
from database.connection import get_pool

logger = structlog.get_logger()

# A run that never finished (the service restarted mid-run) shouldn't block new ones forever
STALE_RUN_MINUTES = 15


class AnalysisBusy(RuntimeError):
    """Another analysis is already running."""


async def get_resume_file(file_id: str | None) -> dict:
    """The requested resume version, or the current one. Raises LookupError if there is none."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        if file_id:
            row = await conn.fetchrow(
                'SELECT "ResumeFileId", "FileName", "Content" FROM "ResumeFiles" WHERE "ResumeFileId" = $1',
                uuid.UUID(file_id))
        else:
            row = await conn.fetchrow(
                'SELECT "ResumeFileId", "FileName", "Content" FROM "ResumeFiles" WHERE "IsCurrent"')
    if row is None:
        raise LookupError("No resume has been uploaded.")
    return {"id": str(row["ResumeFileId"]), "name": row["FileName"], "content": bytes(row["Content"])}


async def save_extracted_text(file_id: str, text: str) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute('UPDATE "ResumeFiles" SET "ExtractedText" = $2 WHERE "ResumeFileId" = $1',
                           uuid.UUID(file_id), text)


async def load_site_data() -> SiteData:
    """Work experience, projects, and information with short aliases (W1, P1, I1), in display order."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        work = await conn.fetch('SELECT * FROM "WorkExperiences" ORDER BY "DisplayOrder"')
        projects = await conn.fetch('SELECT * FROM "Projects" ORDER BY "DisplayOrder"')
        info = await conn.fetch('SELECT "InformationId", "Text" FROM "Information" WHERE "Text" IS NOT NULL '
                                'ORDER BY "InformationId"')
        keywords = await conn.fetch('SELECT "InformationId", "Text" FROM "Keywords" ORDER BY "Text"')

    keywords_of: dict[uuid.UUID, list[str]] = {}
    for k in keywords:
        keywords_of.setdefault(k["InformationId"], []).append(k["Text"])

    return {
        "work": {f"W{i}": {
            "id": str(r["WorkExperienceId"]),
            "employer": r["Employer"],
            "title": r["Title"],
            "start_year": r["StartYear"],
            "end_year": r["EndYear"],
            "summary": r["Summary"],
            "achievements": json.loads(r["Achievements"] or "[]"),
        } for i, r in enumerate(work, start=1)},
        "projects": {f"P{i}": {
            "id": str(r["ProjectId"]),
            "title": r["Title"],
            "role": r["Role"],
            "summary": r["Summary"],
            "detail": r["Detail"],
            "impact_statement": r["ImpactStatement"],
            "tech_stack": json.loads(r["TechStack"] or "[]"),
            "start_year": r["StartYear"],
            "end_year": r["EndYear"],
        } for i, r in enumerate(projects, start=1)},
        "information": {f"I{i}": {
            "id": str(r["InformationId"]),
            "text": r["Text"],
            "keywords": keywords_of.get(r["InformationId"], []),
        } for i, r in enumerate(info, start=1)},
    }


async def start_analysis(file_id: str, model: str) -> str:
    """Records a running analysis. Raises AnalysisBusy if another is genuinely in progress."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        async with conn.transaction():
            await conn.execute(
                """UPDATE "ResumeAnalyses" SET "Status" = 'failed', "CompletedAt" = now(),
                          "Error" = 'The service restarted before this run finished.'
                   WHERE "Status" = 'running' AND "StartedAt" < now() - make_interval(mins => $1)""",
                STALE_RUN_MINUTES)
            if await conn.fetchval('SELECT 1 FROM "ResumeAnalyses" WHERE "Status" = \'running\' LIMIT 1'):
                raise AnalysisBusy("An analysis is already running.")
            analysis_id = uuid.uuid4()
            await conn.execute(
                'INSERT INTO "ResumeAnalyses" ("ResumeAnalysisId", "ResumeFileId", "Status", "Model", "StartedAt") '
                "VALUES ($1, $2, 'running', $3, now())",
                analysis_id, uuid.UUID(file_id), model)
    return str(analysis_id)


async def finish_analysis(analysis_id: str, suggestions: list[Suggestion], summary: str) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        async with conn.transaction():
            for order, s in enumerate(suggestions):
                await conn.execute(
                    """INSERT INTO "ResumeSuggestions"
                           ("ResumeSuggestionId", "ResumeAnalysisId", "Action", "EntityType", "EntityId", "Label",
                            "Changes", "Rationale", "Evidence", "Status", "DisplayOrder")
                       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 'pending', $10)""",
                    uuid.uuid4(), uuid.UUID(analysis_id), s.action, s.entity_type,
                    uuid.UUID(s.entity_id) if s.entity_id else None, s.label,
                    json.dumps(s.changes), s.rationale, s.evidence, order)
            await conn.execute(
                """UPDATE "ResumeAnalyses" SET "Status" = 'completed', "CompletedAt" = now(), "Summary" = $2
                   WHERE "ResumeAnalysisId" = $1""",
                uuid.UUID(analysis_id), summary)


async def fail_analysis(analysis_id: str, error: str) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            """UPDATE "ResumeAnalyses" SET "Status" = 'failed', "CompletedAt" = now(), "Error" = $2
               WHERE "ResumeAnalysisId" = $1""",
            uuid.UUID(analysis_id), error[:1000])
