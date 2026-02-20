namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public string Employer { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public List<string> TechStack { get; set; } = [];
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
}
