from typing import Literal, TypedDict

from pydantic import BaseModel, Field

# What the LLM may propose, per entity. Every field is nullable: null means "leave as is"
# on an update. The structured-output schema is strict, so these are required-but-nullable
# rather than optional.

PRESENT = "present"  # end_year value for a role that hasn't ended (the database stores null)


class ListEdit(BaseModel):
    """How to change a list field. Existing entries stay unless they are named in remove."""

    add: list[str] = Field(description="Entries to add that the resume supports")
    remove: list[str] = Field(
        description="Existing entries to remove, copied exactly from the site data. Only entries the "
                    "resume contradicts. Usually empty.")


# ---- compare: work experience --------------------------------------------

class WorkFields(BaseModel):
    employer: str | None
    title: str | None
    start_year: str | None = Field(description="Four-digit year, like the existing rows")
    end_year: str | None = Field(description=f"Four-digit year, or '{PRESENT}' if the role hasn't ended")
    summary: str | None
    achievements: ListEdit | None


class WorkSuggestion(BaseModel):
    action: Literal["add", "update"]
    target: str | None = Field(description="Alias of the site row to change (W3), or null for an add")
    fields: WorkFields
    rationale: str = Field(description="One sentence: what differs and why the change is warranted")
    evidence: str = Field(description="One contiguous quote, copied exactly from the resume, supporting it")


class WorkComparison(BaseModel):
    suggestions: list[WorkSuggestion]


# ---- compare: projects ----------------------------------------------------

class ProjectFields(BaseModel):
    title: str | None
    role: str | None
    summary: str | None
    detail: str | None
    impact_statement: str | None
    tech_stack: ListEdit | None
    start_year: str | None
    end_year: str | None = Field(description=f"Four-digit year, or '{PRESENT}' if ongoing")


class ProjectSuggestion(BaseModel):
    action: Literal["add", "update"]
    target: str | None = Field(description="Alias of the site row to change (P3), or null for an add")
    fields: ProjectFields
    rationale: str
    evidence: str


class ProjectComparison(BaseModel):
    suggestions: list[ProjectSuggestion]


# ---- compare: information -------------------------------------------------

class InformationFields(BaseModel):
    text: str | None = Field(description="First person, in the voice of the existing entries")
    keywords: ListEdit | None


class InformationSuggestion(BaseModel):
    action: Literal["add", "update"]
    target: str | None = Field(description="Alias of the site entry to change (I3), or null for an add")
    fields: InformationFields
    rationale: str
    evidence: str


class InformationComparison(BaseModel):
    suggestions: list[InformationSuggestion]


# ---- validated output -----------------------------------------------------

EntityType = Literal["WorkExperience", "Project", "Information"]


class Suggestion(BaseModel):
    """A suggestion that passed validation. Ids are real, the quote is in the resume, and
    changes holds only fields that actually differ: {field: {"from": ..., "to": ...}}."""

    action: Literal["add", "update"]
    entity_type: EntityType
    entity_id: str | None  # None for an add
    label: str
    changes: dict[str, dict]
    rationale: str
    evidence: str


class SiteData(TypedDict):
    """The site's current data, with the short aliases the LLM refers to rows by."""

    work: dict[str, dict]
    projects: dict[str, dict]
    information: dict[str, dict]


class ResumeAnalysisState(TypedDict, total=False):
    resume_file_id: str | None   # input: which version, or None for the current one
    resume_text: str
    site: SiteData
    proposals: dict[str, list]   # the LLM's raw suggestions, by section: work, projects, information
    suggestions: list[Suggestion]
    dropped: list[str]           # why proposals were discarded, for logs and the summary

