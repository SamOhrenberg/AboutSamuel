namespace PortfolioWebsite.Api.Dtos;

public class SamuelLMResponse
{
    public required string Message { get; set; }
    public bool DisplayResume { get; set; } = false;
    public string? RedirectToPage { get; set; } = null;
}
