namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// One Job Fit analysis a visitor ran. Requirements are stored as their own rows
/// so "which must-haves keep coming up with no evidence" is a simple GROUP BY.
/// </summary>
public class JobFitRun
{
    public Guid JobFitRunId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public float ResponseTookMs { get; set; }
    public Guid? SessionTrackingId { get; set; }

    public string JobDescription { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Company { get; set; }

    // False when the input wasn't a job description (or the run failed before extraction)
    public bool IsJobDescription { get; set; }

    public string? Rating { get; set; }
    public double? FitScore { get; set; }
    public string? MustHavesMet { get; set; }
    public string? Summary { get; set; }  // JSON: { headline, strengths, gaps }
    public string? CoverLetter { get; set; }

    // Set when the run stopped early: not a job description, agent error, visitor left
    public string? Error { get; set; }

    public virtual ICollection<JobFitRequirement> Requirements { get; set; } = [];
}
