using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PortfolioWebsite.Api.Services;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// The public server can't change the database. The layers, and where each is tested:
///   - every tool is annotated ReadOnly and not Destructive: McpServerTests
///   - PublicPortfolioDbContext.SaveChanges throws: ReadOnlyDataLayerTests
///   - the mcp_readonly role denies writes and private tables: ReadOnlyDataLayerTests
///   - running every tool leaves every table untouched: here
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NoToolCanWriteTests(ApiFactory factory) : IAsyncLifetime
{
    /// <summary>One realistic call per tool. Must cover McpServerTests.ToolNames exactly.</summary>
    private static readonly Dictionary<string, Dictionary<string, object?>> SampleArgs = new()
    {
        ["get_profile"] = [],
        ["list_projects"] = new() { ["tech"] = "C#", ["year"] = 2024, ["pageSize"] = 25 },
        ["get_project"] = new() { ["slugOrId"] = "portfolio-chat" },
        ["get_skills"] = new() { ["minProjects"] = 1 },
        ["get_experience"] = [],
        ["search_portfolio"] = new() { ["query"] = "chat about my work", ["limit"] = 10 },
        ["find_evidence"] = new() { ["requirements"] = new[] { "C# APIs", "PostgreSQL", "led a team" }, ["perRequirement"] = 3 },
    };

    private FakeEmbeddingService Embedder => (FakeEmbeddingService)factory.Services.GetRequiredService<IEmbeddingService>();

    public async Task InitializeAsync()
    {
        await PortfolioSeed.ResetAsync(factory);
        Embedder.Fail = false;
    }

    public Task DisposeAsync()
    {
        Embedder.Fail = false;
        return Task.CompletedTask;
    }

    [Fact]
    public void Sample_args_cover_every_tool()
    {
        Assert.Equal(McpServerTests.ToolNames.Order(), SampleArgs.Keys.Order());
    }

    [Fact]
    public async Task Running_every_tool_leaves_every_table_unchanged()
    {
        var before = await SnapshotAsync();
        Assert.Contains(before, t => t.Key == "\"Projects\"" && t.Value.Rows > 0);

        await using (var client = await McpTestClient.ConnectAsync(factory))
        {
            foreach (var (tool, args) in SampleArgs)
                await client.CallStructuredAsync<object>(tool, args);

            // The search tools again on the full-text path.
            Embedder.Fail = true;
            await client.CallStructuredAsync<object>("search_portfolio", SampleArgs["search_portfolio"]);
            await client.CallStructuredAsync<object>("find_evidence", SampleArgs["find_evidence"]);

            await client.GetPromptAsync("evaluate_candidate_fit",
                new Dictionary<string, object?> { ["jobDescription"] = "Senior C# engineer with PostgreSQL." });
        }

        var after = await SnapshotAsync();
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Per table in public: row count plus a hash of every row's xmin and ctid. Any insert or
    /// update gives a row a new xmin, and a delete changes the count or the ctid list.
    /// </summary>
    private async Task<SortedDictionary<string, (long Rows, string Versions)>> SnapshotAsync()
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
            "SELECT quote_ident(table_name) FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));

        Assert.True(tables.Count > 10, $"Expected the whole schema, found {tables.Count} tables");

        var snapshot = new SortedDictionary<string, (long, string)>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT count(*), coalesce(md5(string_agg(xmin::text || ':' || ctid::text, ',' ORDER BY ctid)), '') FROM {table}",
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            snapshot[table] = (reader.GetInt64(0), reader.GetString(1));
        }
        return snapshot;
    }
}
