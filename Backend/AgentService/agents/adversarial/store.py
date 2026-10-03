"""
Writes adversarial runs to Postgres. The tables are defined by the C# API's EF
migrations (AddAdversarialRuns), so column names here must match those models.
"""
import json
import uuid
from datetime import datetime, timezone

from agents.adversarial.state import CaseVerdict
from database.connection import get_pool


def _now() -> datetime:
    return datetime.now(timezone.utc)


async def create_run(target_model: str, judge_model: str) -> uuid.UUID:
    run_id = uuid.uuid4()
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            """
            INSERT INTO "AdversarialRuns"
                ("AdversarialRunId", "StartedAt", "Status", "TargetModel", "JudgeModel",
                 "TotalCases", "CasesAnswered", "CasesJudged", "Passed", "Scored")
            VALUES ($1, $2, 'running', $3, $4, 0, 0, 0, 0, 0)
            """,
            run_id, _now(), target_model, judge_model,
        )
    return run_id


async def update_progress(run_id: uuid.UUID, *, total: int | None = None,
                          answered: int | None = None, judged: int | None = None) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            """
            UPDATE "AdversarialRuns" SET
                "TotalCases"    = COALESCE($2, "TotalCases"),
                "CasesAnswered" = COALESCE($3, "CasesAnswered"),
                "CasesJudged"   = COALESCE($4, "CasesJudged")
            WHERE "AdversarialRunId" = $1
            """,
            run_id, total, answered, judged,
        )


async def complete_run(run_id: uuid.UUID, verdicts: list[CaseVerdict], summary: dict) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        async with conn.transaction():
            await conn.executemany(
                """
                INSERT INTO "AdversarialCaseResults"
                    ("AdversarialCaseResultId", "AdversarialRunId", "CaseKey", "Category", "Source",
                     "Prompt", "History", "PlantedClaim", "Answer", "ToolCalls",
                     "BlockedByContentFilter", "DurationMs", "Verdict", "FailureType", "Severity",
                     "PremiseHandling", "Claims", "Explanation")
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18)
                """,
                [
                    (
                        uuid.uuid4(), run_id, v.result.case.id, v.result.case.category, v.result.case.source,
                        v.result.case.prompt, json.dumps(v.result.case.history), v.result.case.planted_claim,
                        v.result.answer, json.dumps([t.model_dump() for t in v.result.tool_calls]),
                        v.result.blocked_by_content_filter, v.result.duration_ms, v.verdict, v.failure_type,
                        v.severity, v.premise_handling, json.dumps([c.model_dump() for c in v.claims]),
                        v.explanation,
                    )
                    for v in verdicts
                ],
            )
            await conn.execute(
                """
                UPDATE "AdversarialRuns" SET
                    "Status" = 'completed', "FinishedAt" = $2, "Passed" = $3, "Scored" = $4,
                    "PassRate" = $5, "Summary" = $6, "CasesJudged" = "TotalCases"
                WHERE "AdversarialRunId" = $1
                """,
                run_id, _now(), summary["passed"], summary["scored"], summary["pass_rate"],
                json.dumps(summary["by_category"]),
            )


async def fail_run(run_id: uuid.UUID, error: str) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        await conn.execute(
            """UPDATE "AdversarialRuns" SET "Status" = 'failed', "FinishedAt" = $2, "Error" = $3
               WHERE "AdversarialRunId" = $1""",
            run_id, _now(), error[:2000],
        )


async def mark_interrupted_runs() -> int:
    """On startup: anything still 'running' died with the previous process."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        result = await conn.execute(
            """UPDATE "AdversarialRuns" SET "Status" = 'failed', "FinishedAt" = $1,
                   "Error" = 'Interrupted: the agent service restarted during the run.'
               WHERE "Status" = 'running'""",
            _now(),
        )
    return int(result.split()[-1])
