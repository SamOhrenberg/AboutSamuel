using System.Globalization;
using System.Text;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Derives URL-safe slugs from titles at runtime (there's no slug column).
/// </summary>
public static class Slugger
{
    public const int MaxLength = 80;
    public const string Fallback = "item";

    /// <summary>Lowercase ascii letters and digits joined by single hyphens. Accents are
    /// stripped ("Café" becomes "cafe"); anything else becomes a separator.</summary>
    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fallback;

        var builder = new StringBuilder(text.Length);
        var pendingHyphen = false;

        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or (>= 'A' and <= 'Z'))
            {
                if (pendingHyphen && builder.Length > 0)
                    builder.Append('-');
                pendingHyphen = false;
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength];
            var lastHyphen = slug.LastIndexOf('-');
            if (lastHyphen > 0)
                slug = slug[..lastHyphen];
        }

        return slug.Length == 0 ? Fallback : slug;
    }

    /// <summary>
    /// Assigns each item a unique slug. Pass items in a stable order (the earlier item keeps
    /// the plain slug). Natural slugs are claimed first, so a title that slugs to "foo-2"
    /// keeps it and a duplicate "foo" moves on to "foo-3".
    /// </summary>
    public static IReadOnlyDictionary<TKey, string> AssignUnique<TKey>(
        IEnumerable<(TKey Key, string? Title)> orderedItems) where TKey : notnull
    {
        var items = orderedItems.Select(i => (i.Key, Slug: Slugify(i.Title))).ToList();
        var result = new Dictionary<TKey, string>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<(TKey Key, string Slug)>();

        foreach (var item in items)
        {
            if (claimed.Add(item.Slug))
                result[item.Key] = item.Slug;
            else
                duplicates.Add(item);
        }

        foreach (var (key, slug) in duplicates)
        {
            var n = 2;
            while (!claimed.Add($"{slug}-{n}"))
                n++;
            result[key] = $"{slug}-{n}";
        }

        return result;
    }
}
