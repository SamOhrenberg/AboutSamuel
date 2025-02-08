namespace PortfolioWebsite.Api.Data.Models;

public class Information
{
    public Guid InformationId { get; set; } = Guid.NewGuid();
    public string? Text { get; set; } = null;
    public virtual List<Keyword> Keywords { get; set; } = [];

    public Information() { }
}
