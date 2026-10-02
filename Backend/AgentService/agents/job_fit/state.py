from typing import Literal, TypedDict

from pydantic import BaseModel, Field


# ---- extract_requirements -------------------------------------------------

class ExtractedRequirement(BaseModel):
    """One requirement as the LLM pulls it out of the job description."""

    text: str = Field(description="The requirement, short and specific, e.g. '5+ years of C#/.NET'")
    category: Literal["technical_skill", "experience", "education", "certification", "soft_skill", "domain"]
    importance: Literal["must_have", "nice_to_have"]
    search_query: str = Field(
        description="A short phrase for searching Samuel's experience for evidence of this requirement"
    )


class RequirementsExtraction(BaseModel):
    """Structured output of the extract_requirements step."""

    is_job_description: bool = Field(
        description="False if the input is not a job posting or job description"
    )
    job_title: str | None
    company: str | None
    requirements: list[ExtractedRequirement]


class Requirement(ExtractedRequirement):
    """A requirement once the code has given it a stable id for later steps to cite."""

    id: str


# ---- retrieve_evidence ----------------------------------------------------

class Evidence(BaseModel):
    """A piece of Samuel's history that might support a requirement."""

    id: str = Field(description="'<entity_type>:<database id>', e.g. 'project:3f2a...'")
    entity_type: Literal["information", "project", "work"]
    label: str | None  # project title or employer, None for information entries
    content: str
    score: float  # cosine similarity, higher is closer


# ---- assess_fit -----------------------------------------------------------

AssessmentStatus = Literal["strong", "partial", "no_evidence"]


class LlmCitation(BaseModel):
    alias: str = Field(description="Evidence alias, e.g. 'E3'")
    supports: str = Field(description="A few words on what this evidence shows for the requirement")


class LlmRequirementAssessment(BaseModel):
    """The LLM's verdict on one requirement. Cites evidence by its short alias (E1, E2...)."""

    requirement_id: str
    status: AssessmentStatus
    # Making each citation say what it shows stops the model from padding
    # citations with every candidate it was given
    citations: list[LlmCitation]
    reason: str = Field(description="One sentence explaining the verdict, about Samuel in third person")


class FitSummary(BaseModel):
    headline: str = Field(description="One sentence overall take on the fit")
    strengths: list[str] = Field(description="2 to 4 strongest matches")
    gaps: list[str] = Field(description="0 to 3 honest gaps, phrased as not found in the portfolio")


class LlmFitAssessment(BaseModel):
    """Structured output of the assess_fit step."""

    assessments: list[LlmRequirementAssessment]
    summary: FitSummary


class Citation(BaseModel):
    evidence_id: str  # real id, e.g. 'project:3f2a...'
    supports: str


class Assessment(BaseModel):
    """A validated assessment. Citations are real evidence that was actually retrieved."""

    requirement_id: str
    status: AssessmentStatus
    citations: list[Citation]
    reason: str


FitRating = Literal["strong_fit", "good_fit", "partial_fit", "weak_fit"]


# ---- graph state ----------------------------------------------------------

class JobFitState(TypedDict, total=False):
    job_description: str
    job_title: str | None
    company: str | None
    is_job_description: bool
    requirements: list[Requirement]
    evidence: dict[str, list[Evidence]]  # requirement id -> best matches
    assessments: list[Assessment]
    summary: FitSummary
    rating: FitRating
    fit_score: float  # 0 to 1, must-haves weighted double
    must_haves_met: str  # e.g. "8/10", strong or partial
    cover_letter: str | None  # None when nothing was verified to write about
