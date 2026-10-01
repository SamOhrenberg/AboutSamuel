using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace PortfolioWebsite.Api.Services;

/// <summary>
/// Proxies AI requests to the Python LangGraph agent service.
/// All LLM logic lives in the agent service — this class is purely transport.
/// </summary>
public class AgentServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AgentServiceClient> _logger;

    public AgentServiceClient(HttpClient http, ILogger<AgentServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public record StreamChunk
    {
        public string? Token { get; init; }
        public StreamMeta? Meta { get; init; }
        public bool IsToken => Token != null;
        public bool IsMeta => Meta != null;
    }

    public record StreamMeta(
        string? RedirectToPage = null,
        bool Error = false,
        string? FullResponse = null);

    private record AgentChatRequest(
        string Message,
        List<HistoryMessage> History,
        string? SessionTrackingId);

    private record HistoryMessage(string Role, string Content);

    /// <summary>
    /// Streams tokens from the Python agent service back to the caller.
    /// Parses SSE events and yields structured StreamChunk objects.
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatLog chatLog,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestBody = new AgentChatRequest(
            Message: chatLog.Message,
            History: chatLog.History
                .Select(h => new HistoryMessage(h.Role, h.Content))
                .ToList(),
            SessionTrackingId: chatLog.UserTrackingId?.ToString());

        HttpResponseMessage? response = null;
        string? requestError = null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/chat/stream")
            {
                Content = JsonContent.Create(requestBody)
            };

            response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to agent service");
            requestError = "I'm having trouble connecting right now. Please try again.";
        }

        if (requestError != null)
        {
            yield return new StreamChunk { Token = requestError };
            yield return new StreamChunk { Meta = new StreamMeta(Error: true) };
            yield break;
        }

        await using var stream = await response!.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? streamError = null;

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            string? line;

            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading stream from agent service");
                streamError = "Stream interrupted. Please try again.";
                break;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var json = line["data: ".Length..];

            StreamChunk? chunk = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("token", out var tokenEl))
                {
                    var token = tokenEl.GetString();
                    if (!string.IsNullOrEmpty(token))
                        chunk = new StreamChunk { Token = token };
                }
                else if (root.TryGetProperty("meta", out var metaEl))
                {
                    var redirect = metaEl.TryGetProperty("redirect_to_page", out var r)
                        ? r.GetString() : null;
                    var error = metaEl.TryGetProperty("error", out var err)
                        && err.GetBoolean();
                    _logger.LogDebug("Meta JSON received: {Json}", metaEl.GetRawText());
                    var fullResponse = metaEl.TryGetProperty("full_response", out var fr)
                        ? fr.GetString() : null;

                    chunk = new StreamChunk
                    {
                        Meta = new StreamMeta(redirect, error, fullResponse)
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE chunk: {Json}", json);
                continue;
            }

            if (chunk != null)
                yield return chunk;
        }

        if (streamError != null)
        {
            yield return new StreamChunk { Token = streamError };
            yield return new StreamChunk { Meta = new StreamMeta(Error: true) };
        }
    }
}
