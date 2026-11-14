using ModelContextProtocol;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class PortfolioSearchToolsTests
{
    private readonly FakeSearchService _service = new();
    private PortfolioSearchTools Tools => new(_service);

    [Fact]
    public async Task Search_trims_and_defaults_to_five_results_of_every_type()
    {
        await Tools.SearchPortfolio("  event sourcing ");

        Assert.Equal("event sourcing", _service.Query);
        Assert.Equal(5, _service.Limit);
        Assert.Equal(SearchTypes.All, _service.Types);
    }

    [Fact]
    public async Task Search_types_are_case_insensitive_and_deduplicated()
    {
        await Tools.SearchPortfolio("x", limit: 10, types: [" Project", "note", "PROJECT"]);

        Assert.Equal(["project", "note"], _service.Types);
        Assert.Equal(10, _service.Limit);
    }

    [Theory]
    [InlineData(null, 5, "query is required.")]
    [InlineData("   ", 5, "query is required.")]
    [InlineData("ok", 0, "limit must be between 1 and 10 (got 0).")]
    [InlineData("ok", 11, "limit must be between 1 and 10 (got 11).")]
    public async Task Search_rejects_bad_input(string? query, int limit, string message)
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.SearchPortfolio(query!, limit));

        Assert.Equal(message, ex.Message);
        Assert.Null(_service.Query);
    }

    [Fact]
    public async Task Search_rejects_a_long_query()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.SearchPortfolio(new string('q', 301)));

        Assert.Equal("query must be at most 300 characters (got 301).", ex.Message);
    }

    [Fact]
    public async Task Search_rejects_an_unknown_type()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.SearchPortfolio("x", types: ["project", "chats"]));

        Assert.Equal("types may only contain project, experience, note (got 'chats').", ex.Message);
    }

    [Fact]
    public async Task Evidence_drops_blanks_and_duplicates_and_defaults_to_three()
    {
        await Tools.FindEvidence([" Kubernetes ", "", "kubernetes", "PostgreSQL", "  "]);

        Assert.Equal(["Kubernetes", "PostgreSQL"], _service.Requirements);
        Assert.Equal(3, _service.PerRequirement);
    }

    [Fact]
    public async Task Evidence_accepts_fifteen_requirements()
    {
        await Tools.FindEvidence(Enumerable.Range(1, 15).Select(i => $"skill {i}").ToArray(), perRequirement: 1);

        Assert.Equal(15, _service.Requirements!.Count);
        Assert.Equal(1, _service.PerRequirement);
    }

    [Fact]
    public async Task Evidence_rejects_sixteen_requirements()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() =>
            Tools.FindEvidence(Enumerable.Range(1, 16).Select(i => $"skill {i}").ToArray()));

        Assert.Equal("requirements may contain at most 15 items (got 16).", ex.Message);
    }

    [Fact]
    public async Task Evidence_rejects_a_long_requirement_by_index()
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.FindEvidence(["fine", new string('r', 301)]));

        Assert.Equal("requirements[1] must be at most 300 characters (got 301).", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { new[] { " ", "" } })]
    public async Task Evidence_needs_at_least_one_requirement(string[]? requirements)
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.FindEvidence(requirements!));

        Assert.Equal("requirements must contain at least one non-blank item.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Evidence_rejects_per_requirement_out_of_range(int per)
    {
        var ex = await Assert.ThrowsAsync<McpException>(() => Tools.FindEvidence(["x"], per));

        Assert.Equal($"perRequirement must be between 1 and 3 (got {per}).", ex.Message);
        Assert.Null(_service.Requirements);
    }

    private sealed class FakeSearchService : IPortfolioSearchService
    {
        public string? Query { get; private set; }
        public int Limit { get; private set; }
        public IReadOnlyCollection<string>? Types { get; private set; }
        public IReadOnlyList<string>? Requirements { get; private set; }
        public int PerRequirement { get; private set; }

        public Task<SearchResult> SearchAsync(string query, int limit, IReadOnlyCollection<string> types,
            CancellationToken cancellationToken = default)
        {
            (Query, Limit, Types) = (query, limit, types);
            return Task.FromResult(new SearchResult(query, SearchModes.Semantic, [], 0));
        }

        public Task<EvidenceResult> FindEvidenceAsync(IReadOnlyList<string> requirements, int perRequirement,
            CancellationToken cancellationToken = default)
        {
            (Requirements, PerRequirement) = (requirements, perRequirement);
            return Task.FromResult(new EvidenceResult(SearchModes.Semantic, []));
        }
    }
}
