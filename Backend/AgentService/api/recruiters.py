import uuid

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel

from agents.recruiter.merge import TriageBusy, merge_postings
from api.adversarial import require_internal_secret

router = APIRouter(prefix="/recruiters", tags=["recruiters"], dependencies=[Depends(require_internal_secret)])


class MergeRequest(BaseModel):
    targetId: uuid.UUID
    sourceIds: list[uuid.UUID]


@router.post("/postings/merge", status_code=204)
async def merge(request: MergeRequest):
    """Samuel marked these postings as the same job. Called by the C# admin API."""
    try:
        await merge_postings(request.targetId, request.sourceIds)
    except TriageBusy:
        raise HTTPException(status_code=409, detail="Triage is running. Try again in a minute.")
    except LookupError as e:
        raise HTTPException(status_code=404, detail=str(e))
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
