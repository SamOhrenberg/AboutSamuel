namespace PortfolioWebsite.Api.Data.Models;

public class Project
{
    public Guid ProjectId { get; set; }
    public Guid? WorkExperienceId { get; set; }
    public virtual WorkExperience? WorkExperience { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ImpactStatement { get; set; }

    /// <summary>JSON array of technology names, e.g. ["C#", "ASP.NET Core", "SQL Server"]</summary>
    public string TechStack { get; set; } = "[]";

    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
}