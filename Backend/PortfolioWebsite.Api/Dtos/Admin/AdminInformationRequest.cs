namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminInformationRequest
{
    public string? Text { get; set; }

    public List<string> Keywords { get; set; } = [];
}
