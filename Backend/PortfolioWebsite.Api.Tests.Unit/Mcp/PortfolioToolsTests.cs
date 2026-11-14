using Microsoft.Extensions.Options;
using ModelContextProtocol;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class PortfolioToolsTests
{
    private static readonly PortfolioUrls Urls = new("https://example.test");
    private readonly FakePortfolioReadService _service = new();

    private PortfolioTools Tools(McpProfileOptions? profile = null) =>
        new(_service, Options.Create(profile ?? new McpProfileOptions { Name = "Samuel" }), Urls);

    [Fact]
    public void Profile_trims_and_drops_blank_entries()
    {
        var result = Tools(new McpProfileOptions
        {
            Name = " Samuel ",
            Headline = "Engineer ",
            Summary = " Builds things.",
            CurrentFocus = ["MCP", " ", " Agents "],
            Availability = "  ",
            Links =
            [
                new() { Label = "GitHub", Url = "https://github.com/x" },
                new() { Label = "", Url = "https://blank-label.test" },
                new() { Label = "Broken", Url = "not a url" },
            ],
        }).GetProfile();

        Assert.Equal("Samuel", result.Name);
        Assert.Equal("Engineer", result.Headline);
        Assert.Equal("Builds things.", result.Summary);
        Assert.Equal(["MCP", "Agents"], result.CurrentFocus);
        Assert.Null(result.Availability);
        Assert.Equal([new ProfileLink("GitHub", "https://github.com/x")], result.Links);
        Assert.Equal("https://example.test/", result.Url);
    }

    [Fact]
    public async Task List_projects_passes_validated_filters_to_the_service()
    {
        await Tools().ListProjects(tech: "  C# ", year: 2023, featuredOnly: true, page: 2, pageSize: 25);

        Assert.Equal(new ProjectQuery("C#", 2023, true, 2, 25), _service.LastQuery);
    }

    [Fact]
    public async Task List_projects_defaults_and_treats_blank_tech_as_no_filter()
    {
        await Tools().ListProjects(tech: "   ");

        Assert.Equal(new ProjectQuery(null, null, false, 1, 10), _service.LastQuery);
    }

    [Theory]
    [InlineData(null, null, 0, 10, "page")]
    [InlineData(null, null, 1001, 10, "page")]
    [InlineData(null, null, 1, 0, "pageSize")]
    [InlineData(null, null, 1, 26, "pageSize")]
    [InlineData(null, 1949, 1, 10, "year")]
    [InlineData(null, 2101, 1, 10, "year")]
    [InlineData("101", null, 1, 10, "tech")]
    public async Task List_projects_rejects_bad_input_before_calling_the_service(
        string? tech, int? year, int page, int pageSize, string expectedName)
    {
        if (tech == "101")
            tech = new string('x', 101);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Tools().ListProjects(tech: tech, year: year, page: page, pageSize: pageSize));

        Assert.StartsWith(expectedName + " must", ex.Message);
        Assert.Null(_service.LastQuery);
    }

    [Fact]
    public async Task Get_project_trims_the_key_and_returns_the_project()
    {
        var result = await Tools().GetProject("  portfolio-chat ");

        Assert.Equal("portfolio-chat", _service.LastSlugOrId);
        Assert.Same(FakePortfolioReadService.Project, result);
    }

    [Fact]
    public async Task Get_project_explains_a_miss()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools().GetProject("nope"));

        Assert.Equal("No project matches 'nope'. Call list_projects for valid slugs.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Get_project_requires_a_key(string? key)
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools().GetProject(key!));

        Assert.Equal("slugOrId is required.", ex.Message);
        Assert.Null(_service.LastSlugOrId);
    }

    [Fact]
    public async Task Get_project_rejects_a_long_key_without_echoing_it()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools().GetProject(new string('x', 201)));

        Assert.Equal("slugOrId must be at most 200 characters (got 201).", ex.Message);
    }

    [Fact]
    public async Task Get_skills_passes_the_minimum_and_counts()
    {
        var result = await Tools().GetSkills(minProjects: 3);

        Assert.Equal(3, _service.LastMinProjects);
        Assert.Equal(FakePortfolioReadService.Skills, result.Skills);
        Assert.Equal(1, result.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Get_skills_rejects_an_out_of_range_minimum(int minProjects)
    {
        await Assert.ThrowsAsync<McpException>(() => Tools().GetSkills(minProjects));

        Assert.Null(_service.LastMinProjects);
    }

    [Fact]
    public async Task Get_experience_wraps_and_counts()
    {
        var result = await Tools().GetExperience();

        Assert.Equal(FakePortfolioReadService.Experience, result.Experience);
        Assert.Equal(1, result.Count);
    }

    private sealed class FakePortfolioReadService : IPortfolioReadService
    {
        public static readonly ProjectDetail Project = new(
            "portfolio-chat", Guid.NewGuid(), "Portfolio Chat", "Lead", "Summary", null, null, [],
            "2024", null, true, true, [], "https://example.test/projects?id=1");

        public static readonly IReadOnlyList<SkillSummary> Skills =
            [new SkillSummary("C#", 3, 2018, 2026, true, [], [])];

        public static readonly IReadOnlyList<ExperienceItem> Experience =
            [new ExperienceItem(Guid.NewGuid(), "Acme", "Engineer", "2021", null, true, null, [], [], "u")];

        public ProjectQuery? LastQuery { get; private set; }
        public string? LastSlugOrId { get; private set; }
        public int? LastMinProjects { get; private set; }

        public Task<PagedResult<ProjectSummary>> ListProjectsAsync(ProjectQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<ProjectSummary>([], query.Page, query.PageSize, 0, false));
        }

        public Task<ProjectDetail?> GetProjectAsync(string slugOrId, CancellationToken cancellationToken = default)
        {
            LastSlugOrId = slugOrId;
            return Task.FromResult(slugOrId == Project.Slug ? Project : null);
        }

        public Task<IReadOnlyList<SkillSummary>> GetSkillsAsync(int minProjects = 1, CancellationToken cancellationToken = default)
        {
            LastMinProjects = minProjects;
            return Task.FromResult(Skills);
        }

        public Task<IReadOnlyList<ExperienceItem>> GetExperienceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Experience);
    }
}
