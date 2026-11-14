using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class SluggerTests
{
    [Theory]
    [InlineData("AboutSamuel Portfolio", "aboutsamuel-portfolio")]
    [InlineData("  Leading and trailing  ", "leading-and-trailing")]
    [InlineData("C# / .NET 10 Migration!", "c-net-10-migration")]
    [InlineData("Job-Fit -- Agent", "job-fit-agent")]
    [InlineData("Café Résumé Naïve", "cafe-resume-naive")]
    [InlineData("Ünïcödé Ångström", "unicode-angstrom")]
    [InlineData("API v2 (beta)", "api-v2-beta")]
    [InlineData("日本語 Project", "project")]
    public void Slugify_produces_lowercase_ascii_hyphenated(string title, string expected) =>
        Assert.Equal(expected, Slugger.Slugify(title));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ---")]
    [InlineData("日本語")]
    public void Slugify_falls_back_when_nothing_usable_remains(string? title) =>
        Assert.Equal(Slugger.Fallback, Slugger.Slugify(title));

    [Fact]
    public void Slugify_truncates_on_a_word_boundary()
    {
        var slug = Slugger.Slugify(string.Join(' ', Enumerable.Repeat("word", 40)));

        Assert.True(slug.Length <= Slugger.MaxLength);
        Assert.False(slug.EndsWith('-'));
        Assert.EndsWith("word", slug);
    }

    [Fact]
    public void AssignUnique_suffixes_collisions_in_order()
    {
        var slugs = Slugger.AssignUnique(new (int, string?)[] { (1, "Chat Bot"), (2, "chat-bot"), (3, "Chat  Bot!") });

        Assert.Equal("chat-bot", slugs[1]);
        Assert.Equal("chat-bot-2", slugs[2]);
        Assert.Equal("chat-bot-3", slugs[3]);
    }

    [Fact]
    public void AssignUnique_lets_a_natural_suffix_keep_its_slug()
    {
        var slugs = Slugger.AssignUnique(new (int, string?)[] { (1, "Foo"), (2, "Foo"), (3, "Foo 2") });

        Assert.Equal("foo", slugs[1]);
        Assert.Equal("foo-2", slugs[3]);
        Assert.Equal("foo-3", slugs[2]);
    }

    [Fact]
    public void AssignUnique_gives_every_item_a_distinct_slug()
    {
        var items = new[] { "A", "a", "A!", "", null, "item", "a-2" }
            .Select((title, i) => (i, title));

        var slugs = Slugger.AssignUnique(items);

        Assert.Equal(7, slugs.Count);
        Assert.Equal(slugs.Count, slugs.Values.Distinct().Count());
    }
}
