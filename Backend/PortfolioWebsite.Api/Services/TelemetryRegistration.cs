using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;
using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Services;

/// <summary>
/// OpenTelemetry traces, metrics and logs to App Insights. Off unless
/// APPLICATIONINSIGHTS_CONNECTION_STRING is set (Railway gets it from
/// infra/scripts/sync-railway-vars.sh), because UseAzureMonitor throws without one.
///
/// What gets traced:
///   - Incoming requests (ASP.NET Core) and outgoing HttpClient calls. Calls through
///     AgentServiceClient carry a W3C traceparent header, so the Python service's spans
///     join the same trace.
///   - Azure OpenAI chat calls (OpenAI.ChatClient): model, tokens, latency. OpenAI .NET 2.1
///     has no embedding spans and no content capture; embeddings still show as HTTP calls.
///   - Npgsql queries.
///   - Public MCP tool calls: the mcp.tool.duration histogram (tags tool, success).
///
/// App Insights has local auth off, so the exporter signs in with an Entra token
/// (Monitoring Metrics Publisher), using the same credential chain as Azure OpenAI.
/// </summary>
public static class TelemetryRegistration
{
    public const string ServiceName = "portfolio-api";

    public static bool AddTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            return false;

        // OpenAI .NET only emits spans and metrics when this experimental switch is on.
        AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);

        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor(options => options.Credential = new DefaultAzureCredential())
            // service.name becomes cloud_RoleName: the node name on the App Insights map.
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing.AddSource("OpenAI.ChatClient", "Npgsql"))
            .WithMetrics(metrics => metrics.AddMeter("OpenAI.ChatClient", McpToolObserver.MeterName));

        // Railway health checks aren't worth a trace each.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health"));

        return true;
    }
}
