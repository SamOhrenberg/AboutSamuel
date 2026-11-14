using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;
using static PortfolioWebsite.Api.Tests.Integration.Infrastructure.PortfolioSeed;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// The MCP server end to end: a real McpClient talking Streamable HTTP to /mcp on the
/// in-memory host, reading seeded data through the mcp_readonly role.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class McpServerTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly string[] ToolNames = ["get_profile", "list_projects", "get_project", "get_skills", "get_experience"];
    private const string SiteBaseUrl = "https://aboutsamuel.com";

    private McpClient _client = null!;

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);

        var http = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpRegistration.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http, NullLoggerFactory.Instance, ownsHttpClient: true);
        _client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync() => await _client.DisposeAsync();

    [Fact]
    public void Identifies_itself_with_instructions()
    {
        Assert.Equal(McpRegistration.ServerName, _client.ServerInfo.Name);
        Assert.Equal(McpRegistration.ServerVersion, _client.ServerInfo.Version);
        Assert.Equal(McpRegistration.Instructions, _client.ServerInstructions);
    }

    [Fact]
    public async Task Exposes_exactly_the_read_only_tools_and_the_prompt()
    {
        var tools = await _client.ListToolsAsync();
        var prompts = await _client.ListPromptsAsync();

        Assert.Equal(ToolNames.Order(), tools.Select(t => t.Name).Order());
        Assert.Equal(["evaluate_candidate_fit"], prompts.Select(p => p.Name));
    }

    [Fact]
    public async Task Every_tool_is_annotated_read_only_with_an_output_schema()
    {
        var tools = await _client.ListToolsAsync();

        Assert.All(tools, tool =>
        {
            var annotations = tool.ProtocolTool.Annotations;
            Assert.NotNull(annotations);
            Assert.True(annotations.ReadOnlyHint, $"{tool.Name} ReadOnlyHint");
            Assert.False(annotations.DestructiveHint, $"{tool.Name} DestructiveHint");
            Assert.True(annotations.IdempotentHint, $"{tool.Name} IdempotentHint");
            Assert.False(annotations.OpenWorldHint, $"{tool.Name} OpenWorldHint");
            Assert.NotNull(tool.ProtocolTool.OutputSchema);
            Assert.False(string.IsNullOrWhiteSpace(tool.Description), $"{tool.Name} description");
        });
    }

    public static TheoryData<string> Tools => new(ToolNames);

    /// <summary>
    /// The advertised contract per tool. A missing snapshot is written and the test fails;
    /// set MCP_UPDATE_SNAPSHOTS=1 to accept intended changes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_contract_matches_snapshot(string name)
    {
        var tool = (await _client.ListToolsAsync()).Single(t => t.Name == name).ProtocolTool;
        var actual = new JsonObject
        {
            ["name"] = tool.Name,
            ["title"] = tool.Title,
            ["annotations"] = JsonSerializer.SerializeToNode(tool.Annotations),
            ["inputSchema"] = JsonSerializer.SerializeToNode(tool.InputSchema),
            ["outputSchema"] = JsonSerializer.SerializeToNode(tool.OutputSchema),
        }.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }).ReplaceLineEndings("\n") + "\n";

        var path = Path.Combine(SnapshotDirectory(), $"{name}.json");
        var update = Environment.GetEnvironmentVariable("MCP_UPDATE_SNAPSHOTS") == "1";
        if (update || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
            Assert.True(update, $"Snapshot {path} was missing and has been created. Review it and rerun.");
            return;
        }

        Assert.Equal((await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n"), actual);
    }

    [Fact]
    public async Task Get_profile_returns_the_committed_profile()
    {
        var profile = await CallAsync<ProfileResult>("get_profile");

        Assert.Equal("Samuel Ohrenberg", profile.Name);
        Assert.Contains(profile.Links, l => l.Url == "https://github.com/SamOhrenberg");
        Assert.Equal($"{SiteBaseUrl}/", profile.Url);
    }

    [Fact]
    public async Task List_projects_pages_in_site_order_with_camel_case_fields()
    {
        var result = await _client.CallToolAsync("list_projects",
            new Dictionary<string, object?> { ["pageSize"] = 2 });

        Assert.NotEqual(true, result.IsError);
        var json = JsonSerializer.SerializeToElement(result.StructuredContent);
        Assert.True(json.TryGetProperty("hasMore", out _));
        Assert.True(json.TryGetProperty("totalCount", out _));

        var page = json.Deserialize<PagedResult<ProjectSummary>>(JsonSerializerOptions.Web)!;
        Assert.Equal(["portfolio-chat", "portfolio-chat-2"], page.Items.Select(p => p.Slug));
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.HasMore);
        Assert.Equal($"{SiteBaseUrl}/projects?id={PortfolioChat}", page.Items[0].Url);
    }

    [Fact]
    public async Task List_projects_filters_by_tech_and_year()
    {
        var page = await CallAsync<PagedResult<ProjectSummary>>("list_projects",
            new() { ["tech"] = "c#", ["year"] = 2019 });

        Assert.Equal(["billing-rewrite"], page.Items.Select(p => p.Slug));
    }

    [Fact]
    public async Task Bad_input_is_a_tool_error_with_a_useful_message()
    {
        var text = await CallErrorAsync("list_projects", new() { ["pageSize"] = 500 });

        // The SDK prefixes "An error occurred invoking 'list_projects': ".
        Assert.EndsWith(": pageSize must be between 1 and 25 (got 500).", text);
    }

    [Fact]
    public async Task Get_project_by_slug_or_id_returns_the_full_write_up()
    {
        var bySlug = await CallAsync<ProjectDetail>("get_project", new() { ["slugOrId"] = "portfolio-chat" });
        var byId = await CallAsync<ProjectDetail>("get_project", new() { ["slugOrId"] = PortfolioChat.ToString() });

        Assert.Equal(PortfolioChat, bySlug.Id);
        Assert.Equal("Agent service plus pgvector.", bySlug.Detail);
        Assert.Equal("Answers recruiter questions.", bySlug.ImpactStatement);
        Assert.Equal(["Acme"], bySlug.Experiences.Select(e => e.Employer));
        Assert.Equal(bySlug, byId with { TechStack = bySlug.TechStack, Experiences = bySlug.Experiences });
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("secret-prototype")]
    public async Task Get_project_misses_and_inactive_projects_are_tool_errors(string slugOrId)
    {
        var text = await CallErrorAsync("get_project", new() { ["slugOrId"] = slugOrId });

        Assert.EndsWith($": No project matches '{slugOrId}'. Call list_projects for valid slugs.", text);
    }

    [Fact]
    public async Task Hidden_project_is_not_reachable_by_id()
    {
        await CallErrorAsync("get_project", new() { ["slugOrId"] = SecretPrototype.ToString() });
    }

    [Fact]
    public async Task Get_skills_aggregates_tech_stacks()
    {
        var result = await CallAsync<SkillsResult>("get_skills", new() { ["minProjects"] = 2 });

        Assert.Equal(["C#", "PostgreSQL"], result.Skills.Select(s => s.Name));
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result.Skills, s => s.Name == "Rust");
    }

    [Fact]
    public async Task Get_experience_lists_active_roles_with_their_projects()
    {
        var result = await CallAsync<ExperienceResult>("get_experience");

        Assert.Equal(["Acme", "Globex"], result.Experience.Select(e => e.Employer));
        Assert.Equal(2, result.Count);
        var acme = result.Experience[0];
        Assert.True(acme.IsCurrent);
        Assert.Equal(["portfolio-chat", "portfolio-chat-2"], acme.Projects.Select(p => p.Slug).Order());
        Assert.Empty(result.Experience[1].Achievements);
    }

    [Fact]
    public async Task Evaluate_candidate_fit_prompt_embeds_the_job_description()
    {
        var result = await _client.GetPromptAsync("evaluate_candidate_fit",
            new Dictionary<string, object?> { ["jobDescription"] = "Senior C# engineer with Postgres." });

        var message = Assert.Single(result.Messages);
        Assert.Equal(Role.User, message.Role);
        var text = Assert.IsType<TextContentBlock>(message.Content).Text;
        Assert.Contains("<job_description>", text);
        Assert.Contains("Senior C# engineer with Postgres.", text);
    }

    private async Task<T> CallAsync<T>(string tool, Dictionary<string, object?>? args = null)
    {
        var result = await _client.CallToolAsync(tool, args);
        Assert.True(result.IsError is not true, $"{tool} failed: {TextOf(result)}");
        return JsonSerializer.SerializeToElement(result.StructuredContent).Deserialize<T>(JsonSerializerOptions.Web)!;
    }

    private async Task<string> CallErrorAsync(string tool, Dictionary<string, object?> args)
    {
        var result = await _client.CallToolAsync(tool, args);
        Assert.True(result.IsError, $"{tool} should have failed");
        return TextOf(result);
    }

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static string SnapshotDirectory([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)!)!, "Snapshots");
}
