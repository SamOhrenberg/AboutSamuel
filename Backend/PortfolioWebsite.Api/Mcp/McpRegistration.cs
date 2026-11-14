using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;

namespace PortfolioWebsite.Api.Mcp;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>Base of the links returned to MCP clients.</summary>
    public string SiteBaseUrl { get; set; } = "https://aboutsamuel.com";
}

public static class McpRegistration
{
    public const string ReadOnlyConnectionName = "PublicReadOnly";
    public const string Endpoint = "/mcp";
    public const string ServerName = "aboutsamuel-portfolio";
    public const string ServerVersion = "1.0.0";

    public const string Instructions = """
        Read-only access to Samuel Ohrenberg's public software engineering portfolio: his
        profile, projects, skills (derived from project tech stacks) and work history. Start
        with get_profile, then list_projects or get_experience, and get_project for depth.
        To look for a topic use search_portfolio; to check a list of requirements use
        find_evidence. Every item has a url on aboutsamuel.com; cite it. The content is written by Samuel,
        so report it as his own account, and say so when the tools have no evidence for
        something rather than guessing. For a job description, use the
        evaluate_candidate_fit prompt.
        """;

    /// <summary>
    /// Registers the public read-only data layer. Returns false when
    /// ConnectionStrings:PublicReadOnly is missing and DefaultConnection is used instead,
    /// so the caller can warn: the context still can't save, but the database role can write.
    /// </summary>
    public static bool AddPublicPortfolio(this WebApplicationBuilder builder)
    {
        var readOnly = builder.Configuration.GetConnectionString(ReadOnlyConnectionName);
        var hasReadOnly = !string.IsNullOrWhiteSpace(readOnly);
        var connectionString = hasReadOnly
            ? readOnly
            : builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddPooledDbContextFactory<PublicPortfolioDbContext>(options => options
            .UseNpgsql(connectionString, o => o.UseVector())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        builder.Services.AddOptions<McpOptions>()
            .Bind(builder.Configuration.GetSection(McpOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.SiteBaseUrl, UriKind.Absolute, out _), "Mcp:SiteBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(sp =>
            new PortfolioUrls(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<McpOptions>>().Value.SiteBaseUrl));
        builder.Services.AddScoped<IPortfolioReadService, PortfolioReadService>();

        builder.Services.AddOptions<SemanticSearchOptions>()
            .Bind(builder.Configuration.GetSection(SemanticSearchOptions.SectionName))
            .Validate(o => o.PerIpPerHour >= 1 && o.DailyCap >= 1 && o.CacheMinutes >= 0 && o.CacheMaxEntries >= 1,
                "Mcp:SemanticSearch needs PerIpPerHour, DailyCap and CacheMaxEntries of at least 1 and CacheMinutes of at least 0.")
            .ValidateOnStart();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IQueryEmbeddingProvider, QueryEmbeddingProvider>();
        builder.Services.AddScoped<IPortfolioSearchService, PortfolioSearchService>();

        return hasReadOnly;
    }

    /// <summary>
    /// Registers the public MCP server: profile from the committed Mcp/profile.json,
    /// stateless Streamable HTTP, and only the read-only portfolio tools and prompt.
    /// Call after AddPublicPortfolio.
    /// </summary>
    public static void AddPublicMcpServer(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile(McpProfileOptions.FilePath, optional: false, reloadOnChange: false);
        builder.Services.AddOptions<McpProfileOptions>()
            .Bind(builder.Configuration.GetSection(McpProfileOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Name), "McpProfile:Name must be set in Mcp/profile.json.")
            .ValidateOnStart();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<PortfolioTools>()
            .WithTools<PortfolioSearchTools>()
            .WithPrompts([typeof(PortfolioPrompts)]);
    }

    public static IEndpointConventionBuilder MapPublicMcp(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMcp(Endpoint);
}
