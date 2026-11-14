using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class JsonStringArrayTests
{
    [Fact]
    public void Parses_a_string_array_trimming_and_dropping_blanks()
    {
        Assert.True(JsonStringArray.TryParse("""[" C# ", "", "  ", "SQL"]""", out var values));
        Assert.Equal(["C#", "SQL"], values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    public void Empty_input_is_valid_and_empty(string? json)
    {
        Assert.True(JsonStringArray.TryParse(json, out var values));
        Assert.Empty(values);
    }

    [Theory]
    [InlineData("C#, SQL")]
    [InlineData("[\"C#\", ")]
    [InlineData("{\"tech\": [\"C#\"]}")]
    [InlineData("\"C#\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void Malformed_or_non_array_input_is_reported_and_empty(string json)
    {
        Assert.False(JsonStringArray.TryParse(json, out var values));
        Assert.Empty(values);
    }

    [Fact]
    public void Non_string_elements_are_skipped()
    {
        Assert.True(JsonStringArray.TryParse("""["C#", 3, null, {"a": 1}, ["x"], true, "Go"]""", out var values));
        Assert.Equal(["C#", "Go"], values);
    }
}
