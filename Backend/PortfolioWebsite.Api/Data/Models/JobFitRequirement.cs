namespace PortfolioWebsite.Api.Data.Models;

public class JobFitRequirement
{
    public Guid JobFitRequirementId { get; set; }
    public Guid JobFitRunId { get; set; }

    public string RequirementKey { get; set; } = string.Empty;  // "r1", "r2"... within the run
    public string Text { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;        // technical_skill, experience, ...
    public string Importance { get; set; } = string.Empty;      // must_have, nice_to_have

    // strong, partial, no_evidence. Null if the run ended before the assessment.
    public string? Status { get; set; }
    public string? Reason { get; set; }
    public string Citations { get; set; } = "[]";               // JSON, as sent to the page
}
