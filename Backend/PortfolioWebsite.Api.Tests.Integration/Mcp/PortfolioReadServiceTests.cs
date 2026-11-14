using Microsoft.Extensions.DependencyInjection;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;
using static PortfolioWebsite.Api.Tests.Integration.Infrastructure.PortfolioSeed;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

[Collection(ApiCollection.Name)]
public sealed class PortfolioReadServiceTests(ApiFactory factory) : IAsyncLifetime, IDisposable
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();

    private IPortfolioReadService Service => _scope.ServiceProvider.GetRequiredService<IPortfolioReadService>();
    private PortfolioUrls Urls => _scope.ServiceProvider.GetRequiredService<PortfolioUrls>();

    public Task InitializeAsync() => ResetAsync(factory);
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task Lists_active_projects_in_site_order_with_unique_slugs()
    {
        var result = await Service.ListProjectsAsync(new ProjectQuery());

        // Featured first, then by employer order (Acme before Globex), then own order.
        Assert.Equal(["portfolio-chat", "portfolio-chat-2", "billing-rewrite"], result.Items.Select(p => p.Slug));
        Assert.Equal(3, result.TotalCount);
        Assert.False(result.HasMore);
        Assert.DoesNotContain(result.Items, p => p.Id == SecretPrototype);
        Assert.All(result.Items, p => Assert.Equal(Urls.Project(p.Id), p.Url));
    }

    [Theory]
    [InlineData("c#", null, false, new[] { "portfolio-chat", "billing-rewrite" })]
    [InlineData("  POSTGRESQL ", null, false, new[] { "portfolio-chat", "portfolio-chat-2" })]
    [InlineData(null, 2019, false, new[] { "billing-rewrite" })]
    [InlineData(null, 2026, false, new[] { "portfolio-chat" })]
    [InlineData(null, null, true, new[] { "portfolio-chat" })]
    [InlineData("C#", 2019, false, new[] { "billing-rewrite" })]
    [InlineData("Rust", null, false, new string[0])]
    public async Task Filters_projects(string? tech, int? year, bool featuredOnly, string[] expected)
    {
        var result = await Service.ListProjectsAsync(new ProjectQuery(tech, year, featuredOnly));

        Assert.Equal(expected, result.Items.Select(p => p.Slug));
    }

    [Fact]
    public async Task Pages_and_clamps_page_size()
    {
        var first = await Service.ListProjectsAsync(new ProjectQuery(PageSize: 2));
        var second = await Service.ListProjectsAsync(new ProjectQuery(Page: 2, PageSize: 2));
        var clamped = await Service.ListProjectsAsync(new ProjectQuery(Page: 0, PageSize: 500));

        Assert.Equal(2, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(["billing-rewrite"], second.Items.Select(p => p.Slug));
        Assert.False(second.HasMore);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(IPortfolioReadService.MaxPageSize, clamped.PageSize);
    }

    [Fact]
    public async Task Gets_a_project_by_slug_or_id()
    {
        var bySlug = await Service.GetProjectAsync("Portfolio-Chat-2");
        var byId = await Service.GetProjectAsync(PortfolioChat.ToString().ToUpperInvariant());

        Assert.Equal(PortfolioChatTwo, bySlug?.Id);
        Assert.NotNull(byId);
        Assert.Equal("portfolio-chat", byId.Slug);
        Assert.Equal("Agent service plus pgvector.", byId.Detail);
        Assert.Equal("Answers recruiter questions.", byId.ImpactStatement);
        Assert.Equal(["C#", "PostgreSQL", "Azure OpenAI"], byId.TechStack);
        Assert.True(byId.IsCurrent);
        Assert.Null(byId.EndYear);
    }

    [Fact]
    public async Task Hides_inactive_employers_from_project_links()
    {
        var billing = await Service.GetProjectAsync("billing-rewrite");

        var experience = Assert.Single(billing!.Experiences);
        Assert.Equal("Globex", experience.Employer);
        Assert.Equal(Urls.Experience(Globex), experience.Url);
    }

    [Theory]
    [InlineData("secret-prototype")]
    [InlineData("b0000000-0000-0000-0000-000000000004")]
    [InlineData("no-such-project")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_or_inactive_projects_are_null(string slugOrId) =>
        Assert.Null(await Service.GetProjectAsync(slugOrId));

    [Fact]
    public async Task Lists_active_experience_with_its_active_projects()
    {
        var experience = await Service.GetExperienceAsync();

        Assert.Equal(["Acme", "Globex"], experience.Select(e => e.Employer));

        var acme = experience[0];
        Assert.True(acme.IsCurrent);
        Assert.Equal(["Cut deploy time 60%", "Led 4 engineers"], acme.Achievements);
        Assert.Equal(["portfolio-chat", "portfolio-chat-2"], acme.Projects.Select(p => p.Slug));
        Assert.Equal(Urls.Experience(Acme), acme.Url);

        var globex = experience[1];
        Assert.False(globex.IsCurrent);
        Assert.Empty(globex.Achievements); // malformed JSON reads as empty, not an error
        Assert.Equal(["billing-rewrite"], globex.Projects.Select(p => p.Slug));
    }

    [Fact]
    public async Task Derives_skills_from_active_projects()
    {
        var skills = await Service.GetSkillsAsync();

        Assert.DoesNotContain(skills, s => s.Name == "Rust");

        var csharp = skills.Single(s => s.Name == "C#");
        Assert.Equal(2, csharp.ProjectCount);
        Assert.Equal(2018, csharp.FirstYear);
        Assert.True(csharp.IsCurrent);
        Assert.Equal(["Acme", "Globex"], csharp.Employers.Select(e => e.Employer));
        Assert.All(csharp.Projects, p => Assert.Equal(Urls.Project(p.Id), p.Url));

        var postgres = skills.Single(s => s.Name == "PostgreSQL");
        Assert.Equal(["portfolio-chat", "portfolio-chat-2"], postgres.Projects.Select(p => p.Slug));

        var common = await Service.GetSkillsAsync(minProjects: 2);
        Assert.Equal(["C#", "PostgreSQL"], common.Select(s => s.Name));
    }
}
