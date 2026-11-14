using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Records every log event a host writes. Register it as an ILoggerProvider in
/// ConfigureTestServices: Serilog's writeToProviders forwards to it after Serilog's own
/// level overrides and filters, so it sees what App Insights would.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<LogLine> _lines = new();
    public IReadOnlyList<LogLine> Lines => [.. _lines];

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);
    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<LogLine> lines) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = (state as IEnumerable<KeyValuePair<string, object?>> ?? [])
                .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Value);
            lines.Enqueue(new(category, logLevel, formatter(state, exception), properties, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}

public sealed record LogLine(string Category, LogLevel Level, string Message,
    IReadOnlyDictionary<string, object?> Properties, Exception? Exception)
{
    /// <summary>Everything about the line that could leak: message, property values, exception.</summary>
    public string Text => string.Join("\n",
        [Message, .. Properties.Select(p => $"{p.Key}={p.Value}"), Exception?.ToString() ?? ""]);
}
