namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminProjectRequest
{
    public Guid? WorkExperienceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ImpactStatement { get; set; }
    public List<string> TechStack { get; set; } = [];
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
}