using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// Usage analytics end to end: every log line the host writes during real MCP calls is
/// captured (through Serilog's writeToProviders, as App Insights gets them), and none may
/// carry the arguments or the caller's IP.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class McpAnalyticsTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Canary = "zebra7781";
    private const string ClientIp = "198.51.100.23";

    private readonly LogCapture _logs = new();
    private WebApplicationFactory<Program> _host = null!;

    public async Task InitializeAsync()
    {
        await PortfolioSeed.ResetAsync(factory);
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddSingleton<ILoggerProvider>(_logs)));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Tool_calls_are_logged_and_measured_without_args_or_ip()
    {
        var points = new ConcurrentQueue<(double Seconds, string Tool, bool Success)>();
        using var listener = ListenForDurations(points);

        await using (var client = await McpTestClient.ConnectAsync(_host, ClientIp))
        {
            await client.CallStructuredAsync<object>("search_portfolio", new() { ["query"] = $"{Canary} chat" });
            await client.CallStructuredAsync<object>("find_evidence", new() { ["requirements"] = new[] { $"{Canary} billing" } });
            await client.CallExpectingErrorAsync("get_project", new() { ["slugOrId"] = $"{Canary}-project" });
            await Assert.ThrowsAnyAsync<McpException>(async () => await client.CallToolAsync($"{Canary}_tool"));
        }

        var calls = _logs.Lines.Where(l => l.Category == typeof(McpToolObserver).FullName).ToList();
        // An invented tool name is recorded as "unknown", never as the client's text.
        Assert.Equal(
            [("search_portfolio", true), ("find_evidence", true), ("get_project", false), (McpToolObserver.UnknownTool, false)],
            calls.Select(c => ((string)c.Properties["McpTool"]!, (bool)c.Properties["McpSuccess"]!)));
        Assert.All(calls, c => Assert.Equal(LogLevel.Information, c.Level));

        Assert.Equal(calls.Select(c => c.Properties["McpTool"]), points.Select(p => (object)p.Tool));
        Assert.All(points, p => Assert.InRange(p.Seconds, 0, 30));

        // Client errors (bad slug, unknown tool) are McpExceptions; the SDK's Error lines for
        // them are filtered out because they echo the arguments.
        // (TestServer also warns that it can't apply the body size metadata; Kestrel can.)
        Assert.DoesNotContain(_logs.Lines, l => l.Category.StartsWith("ModelContextProtocol"));
        Assert.DoesNotContain(_logs.Lines, l => l.Level >= LogLevel.Error);

        // Request logging ran for these calls and recorded no body.
        Assert.Contains(_logs.Lines, l => l.Properties.ContainsKey("StatusCode")
            && Equals(l.Properties.GetValueOrDefault("RequestPath"), McpRegistration.Endpoint));

        Assert.NotEmpty(_logs.Lines);
        Assert.All(_logs.Lines, line =>
        {
            Assert.DoesNotContain(Canary, line.Text);
            Assert.DoesNotContain(ClientIp, line.Text);
        });
    }

    private MeterListener ListenForDurations(ConcurrentQueue<(double, string, bool)> points)
    {
        var meters = _host.Services.GetRequiredService<IMeterFactory>();
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Scope == meters && instrument.Name == McpToolObserver.DurationInstrument)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            var map = tags.ToArray().ToDictionary(t => t.Key, t => t.Value);
            points.Enqueue((value, (string)map["tool"]!, (bool)map["success"]!));
        });
        listener.Start();
        return listener;
    }

}
