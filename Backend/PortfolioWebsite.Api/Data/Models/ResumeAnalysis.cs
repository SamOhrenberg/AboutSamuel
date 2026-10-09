namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// One run of the resume analysis agent: it compares a resume version against the site's
/// work experience, projects, and information, and proposes changes. Written by the agent
/// service (schema lives here so EF migrations own the database), reviewed in the admin panel.
/// </summary>
public class ResumeAnalysis
{
    public Guid ResumeAnalysisId { get; set; }
    public Guid ResumeFileId { get; set; }
    public ResumeFile ResumeFile { get; set; } = null!;

    public string Status { get; set; } = "running";   // running, completed, failed
    public string Model { get; set; } = string.Empty;
    public string? Summary { get; set; }              // one line: what the run found
    public string? Error { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public virtual ICollection<ResumeSuggestion> Suggestions { get; set; } = [];
}

/// <summary>
/// A proposed change to the site data. Nothing is applied until Samuel approves it. Updates
/// carry only the fields that change. Never a removal: a resume leaving something out
/// doesn't mean the site should.
/// </summary>
public class ResumeSuggestion
{
    public Guid ResumeSuggestionId { get; set; }
    public Guid ResumeAnalysisId { get; set; }
    public ResumeAnalysis Analysis { get; set; } = null!;

    public string Action { get; set; } = string.Empty;       // add, update
    public string EntityType { get; set; } = string.Empty;   // WorkExperience, Project, Information
    public Guid? EntityId { get; set; }                      // the row to change; null for an add
    public string Label { get; set; } = string.Empty;        // what the admin sees, e.g. "CampusWorks LLC: Engineer"

    /// <summary>JSON object of field name to { "from": ..., "to": ... }. "from" is null for adds.</summary>
    public string Changes { get; set; } = "{}";

    public string Rationale { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;     // verbatim text from the resume

    public string Status { get; set; } = "pending";          // pending, applied, rejected
    public DateTimeOffset? DecidedAt { get; set; }
    public int DisplayOrder { get; set; }
}
