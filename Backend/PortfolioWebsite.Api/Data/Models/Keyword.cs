namespace PortfolioWebsite.Api.Data.Models;

public class Keyword
{
    public Keyword() { }

    public Keyword(string token, Information information)
    {
        Text = token;
        Information = information;
    }

    public Guid KeywordId { get; set; }
    public string Text { get; set; }
    public virtual Information Information { get; set; }

    public override string ToString()
    {
        return Text;
    }
}
