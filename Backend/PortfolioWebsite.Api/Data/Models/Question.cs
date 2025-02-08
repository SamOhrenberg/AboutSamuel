namespace PortfolioWebsite.Api.Data.Models;

public class Question
{
    public int QuestionId { get; set; }
    public string Text { get; set; }
    public string Answer { get; set; }
    public List<QuestionKeyword> QuestionKeywords { get; set; }
}

public class Keyword
{
    public int KeywordId { get; set; }
    public string Text { get; set; }
    public List<QuestionKeyword> QuestionKeywords { get; set; }
}

public class QuestionKeyword
{
    public int QuestionId { get; set; }
    public Question Question { get; set; }

    public int KeywordId { get; set; }
    public Keyword Keyword { get; set; }
}
