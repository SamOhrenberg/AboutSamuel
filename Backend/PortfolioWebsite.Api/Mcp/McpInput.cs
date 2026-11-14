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
    public const int MaxQueryLength = 300;
    public const int MaxSearchLimit = 10;
    public const int MaxRequirements = 15;
    public const int MaxRequirementLength = 300;
    public const int MaxPerRequirement = 3;

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

    /// <summary>
    /// A subset of the allowed values, lowercased, or all of them when missing or empty.
    /// </summary>
    public static IReadOnlyList<string> OneOf(IReadOnlyList<string>? values, string name, IReadOnlyList<string> allowed)
    {
        if (values is null || values.Count == 0)
            return allowed;

        var chosen = new List<string>();
        foreach (var value in values)
        {
            var key = value?.Trim().ToLowerInvariant() ?? "";
            if (!allowed.Contains(key))
                throw new McpException($"{name} may only contain {string.Join(", ", allowed)} (got '{Preview(value)}').");
            if (!chosen.Contains(key))
                chosen.Add(key);
        }
        return chosen;
    }

    /// <summary>
    /// A list of trimmed texts: blanks dropped, case-insensitive duplicates merged, then at
    /// least one and at most maxItems left, each at most maxLength characters.
    /// </summary>
    public static IReadOnlyList<string> TextList(IReadOnlyList<string>? values, string name, int maxItems, int maxLength)
    {
        var items = new List<string>();
        for (var i = 0; i < (values?.Count ?? 0); i++)
        {
            var trimmed = values![i]?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (trimmed.Length > maxLength)
                throw new McpException($"{name}[{i}] must be at most {maxLength} characters (got {trimmed.Length}).");
            if (!items.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                items.Add(trimmed);
        }

        if (items.Count == 0)
            throw new McpException($"{name} must contain at least one non-blank item.");
        if (items.Count > maxItems)
            throw new McpException($"{name} may contain at most {maxItems} items (got {items.Count}).");
        return items;
    }

    private static string Preview(string? value)
    {
        var text = value ?? "";
        return text.Length <= 30 ? text : text[..30] + "…";
    }
}
