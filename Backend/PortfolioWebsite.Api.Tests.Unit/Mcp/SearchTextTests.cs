using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class SearchTextTests
{
    [Theory]
    [InlineData("distributed systems", "distributed or systems")]
    [InlineData("C# and .NET 8", "C or and or NET or 8")]
    [InlineData("\"exact phrase\" -excluded", "exact or phrase or excluded")]
    [InlineData("Go or Rust OR go", "Go or Rust")]
    [InlineData("Ünïcode café", "Ünïcode or café")]
    public void Full_text_query_ors_the_words(string input, string expected) =>
        Assert.Equal(expected, SearchText.ToFullTextQuery(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  -- \"\" !! ")]
    [InlineData("or OR")]
    public void Full_text_query_is_null_without_words(string? input) =>
        Assert.Null(SearchText.ToFullTextQuery(input));

    [Fact]
    public void Full_text_query_caps_the_number_of_terms()
    {
        var input = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"w{i}"));

        var terms = SearchText.ToFullTextQuery(input)!.Split(" or ");

        Assert.Equal(SearchText.MaxFullTextTerms, terms.Length);
        Assert.Equal("w0", terms[0]);
    }

    [Fact]
    public void Snippet_keeps_short_text_and_collapses_whitespace() =>
        Assert.Equal("one two three", SearchText.Snippet("  one\n two\t\tthree "));

    [Fact]
    public void Snippet_of_null_is_empty() => Assert.Equal("", SearchText.Snippet(null));

    [Fact]
    public void Snippet_cuts_at_a_word_boundary_with_an_ellipsis()
    {
        Assert.Equal("alpha beta…", SearchText.Snippet("alpha beta, gamma delta", 15));
        Assert.Equal("alpha beta, gamma…", SearchText.Snippet("alpha beta, gamma delta", 17));
    }

    [Fact]
    public void Snippet_cuts_mid_word_when_there_is_no_late_space()
    {
        var snippet = SearchText.Snippet("a supercalifragilistic word", 12);

        Assert.Equal("a supercalif…", snippet);
    }
}
