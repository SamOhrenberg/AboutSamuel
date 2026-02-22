namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminInformationDto
{
    public Guid InformationId { get; set; }
    public string? Text { get; set; }
    public List<string> Keywords { get; set; } = [];
    public bool HasEmbedding { get; set; }
}
