namespace PortfolioWebsite.Api.Dtos;

public class ContactRequest
{
    public required string Email { get; set; }
    public string? Message { get; set; }
}