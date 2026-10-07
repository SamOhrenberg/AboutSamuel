"""
Samuel's job preferences. A code default for now; these move to the database and an
admin page once the dry run's decisions look right.
"""
from pydantic import BaseModel


class Preferences(BaseModel):
    # Annual pay floors by work arrangement. Separate so hybrid or onsite can be raised later.
    remote_pay_floor: float = 90_000
    hybrid_pay_floor: float = 90_000
    onsite_pay_floor: float = 90_000

    # Hourly pay is annualized at this many hours to compare against the floors
    hours_per_year: float = 2080

    allow_remote: bool = True
    allow_hybrid: bool = True
    allow_onsite: bool = True

    # Hybrid and onsite roles must be in one of these places (roughly 20 miles of OKC).
    # A town that isn't listed goes to review, never an automatic decline.
    acceptable_locations: list[str] = [
        "Oklahoma City", "OKC", "Edmond", "Moore", "Midwest City", "Del City", "Yukon",
        "Mustang", "Bethany", "Warr Acres", "Nichols Hills", "The Village", "Choctaw", "Norman",
        "Tinker AFB", "Tinker Air Force Base", "Piedmont", "Newcastle", "Spencer", "Harrah", "Jones",
    ]
    home_state: str = "OK"

    allowed_employment_types: list[str] = ["full_time", "contract_to_hire"]
    dealbreaker_contract_terms: list[str] = ["c2c", "1099"]

    # Requirements that don't fit the presets. Checked by the LLM, not by rules.
    free_text_requirements: str = (
        "The role must be primarily hands-on software development or AI engineering, where I'm "
        "writing code. Not desktop support, help desk, manual QA, project management, security "
        "analysis, or BI reporting without real development."
    )


DEFAULT_PREFERENCES = Preferences()
