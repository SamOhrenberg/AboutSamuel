using System.Text.RegularExpressions;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Numeric reading of the free-text StartYear/EndYear columns. Follows the site's convention:
/// a missing EndYear means "Present". End is the current year for ongoing items, and the
/// start year for items with an unreadable end.
/// </summary>
public readonly partial record struct YearRange(int? Start, int? End, bool IsCurrent)
{
    private static readonly string[] PresentWords = ["present", "current", "now", "ongoing"];

    public static YearRange From(string? startYear, string? endYear, int currentYear)
    {
        var start = ParseYear(startYear);
        var isCurrent = string.IsNullOrWhiteSpace(endYear)
            || PresentWords.Contains(endYear.Trim(), StringComparer.OrdinalIgnoreCase);

        var end = isCurrent ? currentYear : ParseYear(endYear) ?? start;
        start ??= isCurrent ? null : end;

        return new YearRange(start, end, isCurrent);
    }

    public bool Contains(int year) => Start is { } s && End is { } e && s <= year && year <= e;

    /// <summary>The first plausible four-digit year (1900-2099) in the text, if any.</summary>
    public static int? ParseYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var match = YearPattern().Match(text);
        return match.Success ? int.Parse(match.Value) : null;
    }

    [GeneratedRegex(@"(?<!\d)(19|20)\d{2}(?!\d)")]
    private static partial Regex YearPattern();
}
