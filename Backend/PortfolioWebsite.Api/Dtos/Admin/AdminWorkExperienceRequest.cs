namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminWorkExperienceRequest
{
    public string Employer { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
    public string? Summary { get; set; }
    public List<string> Achievements { get; set; } = [];
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
