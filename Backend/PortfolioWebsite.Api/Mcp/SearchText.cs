using System.Text.RegularExpressions;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>Pure text helpers for search: full-text queries and snippets.</summary>
public static partial class SearchText
{
    public const int SnippetLength = 300;
    public const int NoteTitleLength = 80;
    public const int MaxFullTextTerms = 32;

    /// <summary>
    /// Turns free text into a websearch_to_tsquery input that ORs the words, so a long
    /// requirement matches rows that share some of its terms (ts_rank puts rows sharing more
    /// first). Only letters and digits survive, which also drops websearch syntax like quotes
    /// and leading minus signs. Null when there are no words.
    /// </summary>
    public static string? ToFullTextQuery(string? text)
    {
        var terms = Word().Matches(text ?? "")
            .Select(m => m.Value)
            // "or" is websearch_to_tsquery's operator, not a term.
            .Where(t => !t.Equals("or", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxFullTextTerms)
            .ToList();

        return terms.Count == 0 ? null : string.Join(" or ", terms);
    }

    /// <summary>Whitespace collapsed and cut at a word boundary, with an ellipsis when cut.</summary>
    public static string Snippet(string? text, int maxLength = SnippetLength)
    {
        var clean = Whitespace().Replace(text ?? "", " ").Trim();
        if (clean.Length <= maxLength)
            return clean;

        var cut = clean[..maxLength];
        // Back up to a space unless the cut already ends a word.
        var space = cut.LastIndexOf(' ');
        if (clean[maxLength] != ' ' && space > maxLength / 2)
            cut = cut[..space];
        return cut.TrimEnd(' ', ',', ';', ':', '.') + "…";
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
