namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminChatSessionDto
{
    public Guid? SessionTrackingId { get; set; }
    public DateTimeOffset FirstMessageAt { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
    public int MessageCount { get; set; }
    public bool HadError { get; set; }
    public bool HadTokenLimit { get; set; }
    public List<AdminChatMessageDto> Messages { get; set; } = [];
}
