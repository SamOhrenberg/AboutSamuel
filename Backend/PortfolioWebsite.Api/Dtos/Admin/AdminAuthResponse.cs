namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminAuthResponse
{
    public required string Jwt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
