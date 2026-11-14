using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public sealed class McpToolObserverTests : IDisposable
{
    private readonly TestMeterFactory _meters = new();
    private readonly RecordingLogger _logger = new();
    private readonly SteppingTimeProvider _time = new(TimeSpan.FromMilliseconds(250));
    private readonly MeterListener _listener = new();
    private readonly List<(double Value, Dictionary<string, object?> Tags)> _points = [];
    private readonly McpToolObserver _observer;

    public McpToolObserverTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == _meters && instrument.Name == McpToolObserver.DurationInstrument)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            _points.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        _listener.Start();

        _observer = new McpToolObserver(_meters, _logger, _time);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _meters.Dispose();
    }

    private static CallToolResult Ok() => new() { Content = [new TextContentBlock { Text = "secret result" }] };

    [Fact]
    public async Task Success_records_the_tool_duration_and_a_log_line()
    {
        var result = await _observer.ObserveAsync("get_project", () => ValueTask.FromResult(Ok()));

        Assert.Equal("secret result", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var (value, tags) = Assert.Single(_points);
        Assert.Equal(0.25, value, 6);
        Assert.Equal("get_project", tags["tool"]);
        Assert.Equal(true, tags["success"]);

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("MCP tool call get_project success True in 250.0 ms", entry.Message);
    }

    [Fact]
    public async Task Tool_error_result_counts_as_a_failure()
    {
        await _observer.ObserveAsync("list_projects", () => ValueTask.FromResult(new CallToolResult { IsError = true }));

        Assert.Equal(false, Assert.Single(_points).Tags["success"]);
        Assert.Contains("success False", Assert.Single(_logger.Entries).Message);
    }

    [Fact]
    public async Task Exception_is_recorded_as_a_failure_and_rethrown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _observer.ObserveAsync("get_skills", () => throw new InvalidOperationException("boom")));

        Assert.Equal(false, Assert.Single(_points).Tags["success"]);
        var entry = Assert.Single(_logger.Entries);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("boom", entry.Message);
    }

    [Fact]
    public async Task Only_the_tool_success_and_duration_are_logged()
    {
        await _observer.ObserveAsync("search_portfolio", () => ValueTask.FromResult(Ok()));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(["McpTool", "McpSuccess", "ElapsedMs", "{OriginalFormat}"], entry.State.Select(p => p.Key));
        Assert.Equal(["tool", "success"], Assert.Single(_points).Tags.Keys);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _created = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name, options.Version, options.Tags, scope: this);
            _created.Add(meter);
            return meter;
        }

        public void Dispose() => _created.ForEach(m => m.Dispose());
    }

    /// <summary>Every GetTimestamp call moves time forward by one step.</summary>
    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks += step.Ticks;
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception,
        IReadOnlyList<KeyValuePair<string, object?>> State);

    private sealed class RecordingLogger : ILogger<McpToolObserver>
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new(logLevel, formatter(state, exception), exception,
                state as IReadOnlyList<KeyValuePair<string, object?>> ?? []));

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
