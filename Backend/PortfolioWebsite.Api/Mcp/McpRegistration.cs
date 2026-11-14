using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Serilog.Events;
using Serilog.Filters;

namespace PortfolioWebsite.Api.Mcp;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>Base of the links returned to MCP clients.</summary>
    public string SiteBaseUrl { get; set; } = "https://aboutsamuel.com";
}

/// <summary>Per-IP fixed window on every request to /mcp (initialize, list and call alike).</summary>
public sealed class McpRateLimitOptions
{
    public const string SectionName = "Mcp:RateLimit";

    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}

public static class McpRegistration
{
    public const string ReadOnlyConnectionName = "PublicReadOnly";
    public const string Endpoint = "/mcp";
    public const string RateLimitPolicy = "Mcp";

    /// <summary>
    /// The largest valid request is evaluate_candidate_fit with a 20,000 character job
    /// description, about 20 KB of JSON for mostly ASCII text. 64 KB leaves room for
    /// escaping and multi-byte characters without accepting arbitrary payloads.
    /// </summary>
    public const long MaxRequestBodyBytes = 64 * 1024;
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

        builder.Services.AddOptions<McpRateLimitOptions>()
            .Bind(builder.Configuration.GetSection(McpRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit >= 1 && o.WindowSeconds >= 1,
                "Mcp:RateLimit needs PermitLimit and WindowSeconds of at least 1.")
            .ValidateOnStart();

        builder.Services.AddSingleton<McpToolObserver>();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<PortfolioTools>()
            .WithTools<PortfolioSearchTools>()
            .WithPrompts([typeof(PortfolioPrompts)])
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpToolObserver.Filter));
    }

    private static readonly Func<LogEvent, bool> FromSdk = Matching.FromSource("ModelContextProtocol");

    /// <summary>
    /// True for the SDK's log events about an McpException: bad input, a missing slug, an
    /// unknown tool. The SDK logs each at Error with a stack trace, and the messages echo the
    /// client's arguments, so Program drops them. McpToolObserver still counts them as failed
    /// calls. Any other exception from a tool is a real fault and keeps its log line.
    /// </summary>
    public static bool IsSdkClientError(LogEvent logEvent) =>
        logEvent.Exception is McpException && FromSdk(logEvent);

    /// <summary>The "Mcp" rate limiter policy: per client IP, limits from Mcp:RateLimit.</summary>
    public static RateLimitPartition<string> RateLimitPartition(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<McpRateLimitOptions>>().Value;
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                QueueLimit = 0,
            });
    }

    /// <summary>
    /// Maps /mcp with the rate limit and the body cap. The cap is applied twice: the metadata
    /// makes Kestrel stop reading any body past the limit (chunked included), and the filter
    /// turns a declared Content-Length over it into a 413 before the SDK reads anything. The
    /// filter is also what the tests can see, since TestServer ignores the metadata.
    /// </summary>
    public static IEndpointConventionBuilder MapPublicMcp(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMcp(Endpoint)
            .RequireRateLimiting(RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
            .AddEndpointFilter((context, next) =>
                context.HttpContext.Request.ContentLength > MaxRequestBodyBytes
                    ? ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status413PayloadTooLarge))
                    : next(context));
}
