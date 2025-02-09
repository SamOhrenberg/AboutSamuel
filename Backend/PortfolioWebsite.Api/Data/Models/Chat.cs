namespace PortfolioWebsite.Api.Data.Models;

public class Chat
{
    public Guid ChatId { get; set; }
    public string History { get; set; }
    public string Message { get; set; }
    public string Response { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public float ResponseTookMs { get; set; }
    public bool TokenLimitReached { get; set; }
    public bool Error { get; set; }
    public Guid? SessionTrackingId { get; set; }
}
