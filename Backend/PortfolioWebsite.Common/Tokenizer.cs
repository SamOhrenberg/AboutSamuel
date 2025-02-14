using System.Text;
using System.Text.RegularExpressions;

namespace PortfolioWebsite.Common;

public static partial class Tokenizer
{
    public static IEnumerable<string> Tokenize(string text)
    {
        string tokenString = text.ToLower();

        tokenString.ReplaceLineEndings(" ");

        tokenString = TrimSpaceEndingPunctuation().Replace(tokenString, " ");
        tokenString = TrimConsecutiveSpacesRegex().Replace(tokenString, " ");

        var tokens = tokenString
                        .Split(' ')
                        .Except(Constants.StopWords)
                        .Where(a => a.Length > 0)
                        .ToArray();

        return tokens;
    }

    [GeneratedRegex(@"[.,!?;:]\s")]
    private static partial Regex TrimSpaceEndingPunctuation();

    [GeneratedRegex(@"\s+")]
    private static partial Regex TrimConsecutiveSpacesRegex();
}