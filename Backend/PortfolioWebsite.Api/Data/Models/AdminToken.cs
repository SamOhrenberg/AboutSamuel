namespace PortfolioWebsite.Api.Data.Models;

public class AdminToken
{
    public Guid AdminTokenId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestIp { get; set; }
}