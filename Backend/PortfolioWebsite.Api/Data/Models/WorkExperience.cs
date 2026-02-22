namespace PortfolioWebsite.Api.Data.Models;

public class WorkExperience
{
    public Guid WorkExperienceId { get; set; }
    public string Employer { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }

    /// <summary>
    /// A concise paragraph summarizing the role and its context.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// JSON array of achievement bullet strings, e.g.
    /// ["Reduced deployment time by 60%", "Led a team of 4 engineers"]
    /// </summary>
    public string Achievements { get; set; } = "[]";

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public string? EmbeddingJson { get; set; }

    public virtual ICollection<Project> Projects { get; set; } = [];
}
