using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        return hasReadOnly;
    }
}
