namespace PortfolioWebsite.Api.Dtos;

public class JobFitRequest
{
    public string JobDescription { get; set; } = string.Empty;
    public Guid? UserTrackingId { get; set; }
}
