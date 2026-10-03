namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// One adversarial test run against SamuelLM. The agent service writes these rows
/// (the schema lives here so EF migrations stay the single owner of the database).
/// </summary>
public class AdversarialRun
{
    public Guid AdversarialRunId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public string Status { get; set; } = "running";  // running, completed, failed
    public string? Error { get; set; }

    public string TargetModel { get; set; } = string.Empty;
    public string JudgeModel { get; set; } = string.Empty;

    // Progress, updated while the run is going so the admin page can poll it
    public int TotalCases { get; set; }
    public int CasesAnswered { get; set; }
    public int CasesJudged { get; set; }

    // Pass rate over cases with a usable verdict (invalid and errored excluded)
    public int Passed { get; set; }
    public int Scored { get; set; }
    public double? PassRate { get; set; }
    public string? Summary { get; set; }  // JSON: per-category counts

    public virtual ICollection<AdversarialCaseResult> Cases { get; set; } = [];
}
