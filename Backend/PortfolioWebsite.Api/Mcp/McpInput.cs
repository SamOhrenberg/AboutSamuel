using ModelContextProtocol;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Argument checks for the public MCP tools. Failures throw McpException, which the SDK
/// returns to the client as an isError result carrying this message, so messages say what
/// was wrong and what is allowed, and never echo more than a short bit of the input.
/// </summary>
public static class McpInput
{
    public const int MaxTextLength = 100;
    public const int MaxSlugOrIdLength = 200;
    public const int MaxJobDescriptionLength = 20_000;
    public const int MinYear = 1950;
    public const int MaxYear = 2100;
    public const int MaxPage = 1000;
    public const int MaxMinProjects = 100;

    /// <summary>Trimmed text, or null when missing or blank.</summary>
    public static string? OptionalText(string? value, string name, int maxLength = MaxTextLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new McpException($"{name} must be at most {maxLength} characters (got {trimmed.Length}).");
        return trimmed;
    }

    public static string RequiredText(string? value, string name, int maxLength = MaxTextLength) =>
        OptionalText(value, name, maxLength)
            ?? throw new McpException($"{name} is required.");

    public static int Range(int value, string name, int min, int max) =>
        value < min || value > max
            ? throw new McpException($"{name} must be between {min} and {max} (got {value}).")
            : value;

    public static int? OptionalRange(int? value, string name, int min, int max) =>
        value is { } v ? Range(v, name, min, max) : null;
}
