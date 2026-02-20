namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminChatMessageDto
{
    public Guid ChatId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public float ResponseTookMs { get; set; }
    public bool Error { get; set; }
    public bool TokenLimitReached { get; set; }
}