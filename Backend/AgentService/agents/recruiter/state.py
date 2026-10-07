from typing import Literal

from pydantic import BaseModel, Field


class RoleDetails(BaseModel):
    """What the email says about the job. Anything not stated stays null or unknown."""

    job_title: str | None
    hiring_company: str | None = Field(description="The company the job is at. Null if the recruiter doesn't name it")
    recruiter_name: str | None
    recruiting_agency: str | None = Field(description="Staffing agency or recruiting firm. Null if the recruiter works for the hiring company")
    work_arrangement: Literal["remote", "hybrid", "onsite", "unknown"]
    location: str | None = Field(description="City/state/region for hybrid or onsite roles, or a remote restriction like 'US only'")
    pay_min: float | None = Field(description="Lower end of the stated pay, normalized: $120k -> 120000, $65/hr -> 65")
    pay_max: float | None
    pay_period: Literal["annual", "hourly", "unknown"]
    employment_type: Literal["full_time", "contract", "contract_to_hire", "part_time", "unknown"]
    contract_terms: Literal["w2", "c2c", "1099", "unknown"] = Field(description="Tax terms for contract roles")
    tech_stack: list[str]
    seniority: str | None = Field(description="As stated, e.g. 'Senior', 'Lead', 'Staff'")
    duties: str | None = Field(description="One or two sentences on what the person in this job actually does day to day")


class EmailTriage(BaseModel):
    """Structured output of the classify-and-extract step."""

    category: Literal["recruiter_outreach", "job_board_or_newsletter", "application_update", "other"]
    personalized: bool = Field(
        description="True if it's written to Samuel specifically (mentions his background or name in a "
                    "non-template way). False for an obvious mass blast"
    )
    roles: list[RoleDetails] = Field(
        description="One entry per distinct job the email pitches (recruiter_outreach) or is about "
                    "(application_update). Empty for other categories"
    )
    confidence: Literal["high", "medium", "low"]
    summary: str = Field(description="One short line describing the email")
