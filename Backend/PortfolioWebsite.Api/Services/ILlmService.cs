namespace PortfolioWebsite.Api.Services;

public record LlmMessage(string Role, string Content);

public record LlmToolDefinition(
    string Name,
    string Description,
    object InputSchema);

public record LlmToolCall(
    string ToolCallId,
    string Name,
    string ArgumentsJson);

public record LlmResponse(
    string? Text,
    IReadOnlyList<LlmToolCall> ToolCalls,
    bool IsToolUse,
    int TotalTokens)
{
    public static LlmResponse TextOnly(string text, int tokens = 0)
        => new(text, [], false, tokens);

    public static LlmResponse WithTools(IReadOnlyList<LlmToolCall> tools, int tokens = 0)
        => new(null, tools, true, tokens);
}

public interface ILlmService
{
    /// <summary>
    /// Single-turn completion, no tools.
    /// </summary>
    Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Single-turn streaming completion, no tools.
    /// Yields tokens as they arrive.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Single-turn completion with tool definitions.
    /// Returns either text or tool calls.
    /// </summary>
    Task<LlmResponse> CompleteWithToolsAsync(
        string systemPrompt,
        IReadOnlyList<LlmMessage> messages,
        IReadOnlyList<LlmToolDefinition> tools,
        int maxTokens,
        CancellationToken ct = default);
}
