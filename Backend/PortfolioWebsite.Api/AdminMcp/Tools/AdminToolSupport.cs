using ModelContextProtocol;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.AdminMcp.Tools;

/// <summary>A list result. Tools return objects, so lists get a wrapper with a count.</summary>
public sealed record ItemList<T>(IReadOnlyList<T> Items, int Count)
{
    public ItemList(IReadOnlyList<T> items) : this(items, items.Count) { }
}

/// <summary>What a write did, plus whether the item's RAG embedding was rebuilt to match.</summary>
public sealed record SavedItem<T>(T Item, bool EmbeddingGenerated);

public sealed record Done(string Message);

/// <summary>
/// Argument checks for the admin tools, and the bridge from AdminResult to tool results.
/// Failures throw McpException, which the SDK returns to the client as an isError result
/// with this message.
/// </summary>
public static class AdminToolSupport
{
    public const int ShortText = 200;
    public const int MediumText = 4_000;
    public const int LongText = 40_000;
    public const int MaxListItems = 100;

    /// <summary>The value, or the failure as a tool error.</summary>
    public static T Unwrap<T>(this AdminResult<T> result, string what) =>
        result.IsOk ? result.Value! : throw Error(result, what);

    public static void Unwrap(this AdminResult result, string what)
    {
        if (!result.IsOk) throw Error(result, what);
    }

    private static McpException Error(AdminResult result, string what) => result.Status switch
    {
        AdminStatus.NotFound => new McpException(result.Message ?? $"No {what} has that id."),
        _ => new McpException(result.Message ?? $"The {what} couldn't be saved."),
    };

    /// <summary>Required text, trimmed.</summary>
    public static string Required(string? value, string name, int maxLength = ShortText) =>
        Optional(value, name, maxLength) ?? throw new McpException($"{name} is required.");

    /// <summary>Trimmed text, or null when missing or blank.</summary>
    public static string? Optional(string? value, string name, int maxLength = ShortText)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new McpException($"{name} must be at most {maxLength:N0} characters (got {trimmed.Length:N0}).");
        return trimmed;
    }

    /// <summary>
    /// For partial updates: null keeps the current value, an empty string clears it, anything
    /// else replaces it.
    /// </summary>
    public static string? Patch(string? value, string? current, string name, int maxLength = ShortText) =>
        value is null ? current : Optional(value, name, maxLength);

    /// <summary>Trimmed, non-blank entries, each checked for length.</summary>
    public static List<string> Strings(IEnumerable<string>? values, string name, int maxLength = ShortText,
        int maxItems = MaxListItems)
    {
        var list = (values ?? []).Select(v => v?.Trim() ?? "").Where(v => v.Length > 0).ToList();
        if (list.Count > maxItems)
            throw new McpException($"{name} can have at most {maxItems} entries (got {list.Count}).");
        var tooLong = list.FirstOrDefault(v => v.Length > maxLength);
        if (tooLong is not null)
            throw new McpException($"Each entry in {name} must be at most {maxLength:N0} characters.");
        return list;
    }

    public static int Range(int value, string name, int min, int max) =>
        value < min || value > max ? throw new McpException($"{name} must be between {min} and {max}.") : value;
}
