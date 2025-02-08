namespace PortfolioWebsite.Api.Data.Models;

public class Keyword
{
    public Guid KeywordId { get; set; } = Guid.NewGuid();
    public string Text { get; set; }
}
