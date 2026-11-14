using System.Diagnostics.Metrics;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Usage analytics for the public tools: one log line and one histogram point per call,
/// carrying only the tool name, whether it succeeded and how long it took. Never the
/// arguments, the result or the caller's IP. The log event's own timestamp is the time.
///
/// Errors are left to the SDK, which already turns any exception from a tool into a
/// generic "An error occurred invoking '&lt;tool&gt;'." result and logs the details server
/// side (McpHardeningTests checks it). Exceptions that escape the tool pipeline still
/// propagate, so the SDK can answer with its generic JSON-RPC error.
/// </summary>
public sealed class McpToolObserver
{
    public const string MeterName = "PortfolioWebsite.Api.Mcp";
    public const string DurationInstrument = "mcp.tool.duration";

    /// <summary>Recorded instead of a name the server doesn't have, so a client can't put
    /// arbitrary text into the logs or create new metric tag values.</summary>
    public const string UnknownTool = "unknown";

    private readonly Histogram<double> _duration;
    private readonly ILogger<McpToolObserver> _logger;
    private readonly TimeProvider _time;

    public McpToolObserver(IMeterFactory meters, ILogger<McpToolObserver> logger, TimeProvider time)
    {
        _duration = meters.Create(MeterName).CreateHistogram<double>(
            DurationInstrument, unit: "s", description: "Duration of public MCP tool calls.");
        _logger = logger;
        _time = time;
    }

    public async ValueTask<CallToolResult> ObserveAsync(string tool, Func<ValueTask<CallToolResult>> call)
    {
        var start = _time.GetTimestamp();
        var success = false;
        try
        {
            var result = await call();
            success = result.IsError is not true;
            return result;
        }
        finally
        {
            var elapsed = _time.GetElapsedTime(start);
            _duration.Record(elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("tool", tool),
                new KeyValuePair<string, object?>("success", success));
            _logger.LogInformation("MCP tool call {McpTool} success {McpSuccess} in {ElapsedMs:0.0} ms",
                tool, success, elapsed.TotalMilliseconds);
        }
    }

    /// <summary>The call-tool filter that routes every call through ObserveAsync.</summary>
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter => next => (context, cancellationToken) =>
    {
        var observer = context.Services?.GetService<McpToolObserver>();
        if (observer is null)
            return next(context, cancellationToken);

        var tool = context.MatchedPrimitive is McpServerTool matched ? matched.ProtocolTool.Name : UnknownTool;
        return observer.ObserveAsync(tool, () => next(context, cancellationToken));
    };
}
