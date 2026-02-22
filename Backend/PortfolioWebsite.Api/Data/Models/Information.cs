namespace PortfolioWebsite.Api.Data.Models;

public class Information
{
    public Guid InformationId { get; set; }
    public string? Text { get; set; } = null;
    public string? EmbeddingJson { get; set; }
    public virtual List<Keyword> Keywords { get; set; } = [];

    public Information() { }

    public Information(string? text, IEnumerable<string> tokens) 
    {
        Text = text;
        foreach (var token in tokens)
        {
            Keywords.Add(new Keyword(token, this));
        }
    }

    public Information(Guid informationId, string? text, List<Keyword> keywords)
    {
        InformationId = informationId;
        Text = text;
        Keywords = keywords;
    }
}
