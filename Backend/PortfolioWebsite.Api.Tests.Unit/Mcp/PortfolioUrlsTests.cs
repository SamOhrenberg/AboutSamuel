using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class PortfolioUrlsTests
{
    private static readonly Guid Id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Theory]
    [InlineData("https://aboutsamuel.com")]
    [InlineData("https://aboutsamuel.com/")]
    [InlineData(" https://aboutsamuel.com// ")]
    public void Normalizes_the_base_url(string baseUrl)
    {
        var urls = new PortfolioUrls(baseUrl);

        Assert.Equal("https://aboutsamuel.com", urls.SiteBaseUrl);
        Assert.Equal("https://aboutsamuel.com/projects?id=0f8fad5b-d9cb-469f-a165-70867728950e", urls.Project(Id));
        Assert.Equal("https://aboutsamuel.com/work-experience?id=0f8fad5b-d9cb-469f-a165-70867728950e", urls.Experience(Id));
    }

    [Fact]
    public void Drops_query_and_fragment_from_the_base() =>
        Assert.Equal("http://localhost:3000", new PortfolioUrls("http://localhost:3000/?x=1#top").SiteBaseUrl);

    [Theory]
    [InlineData("resume", "https://aboutsamuel.com/resume")]
    [InlineData("/skill-map", "https://aboutsamuel.com/skill-map")]
    [InlineData("", "https://aboutsamuel.com/")]
    public void Builds_page_links(string path, string expected) =>
        Assert.Equal(expected, new PortfolioUrls("https://aboutsamuel.com").Page(path));

    [Theory]
    [InlineData("")]
    [InlineData("aboutsamuel.com")]
    [InlineData("/projects")]
    [InlineData("ftp://aboutsamuel.com")]
    [InlineData("javascript:alert(1)")]
    public void Rejects_anything_but_absolute_http(string baseUrl) =>
        Assert.Throws<ArgumentException>(() => new PortfolioUrls(baseUrl));
}
