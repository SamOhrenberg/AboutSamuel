using System.Text.Json;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Reads the JSON string-array columns (TechStack, Achievements) without ever throwing.
/// </summary>
public static class JsonStringArray
{
    /// <summary>
    /// Returns the trimmed, non-blank strings in the array. Null or blank input is a valid
    /// empty list. Malformed JSON or a non-array root gives an empty list and false, so the
    /// caller can log it. Non-string elements are skipped.
    /// </summary>
    public static bool TryParse(string? json, out IReadOnlyList<string> values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            values = document.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
