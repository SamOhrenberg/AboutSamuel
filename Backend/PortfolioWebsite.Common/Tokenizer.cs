using System.Text;
using System.Text.RegularExpressions;

namespace PortfolioWebsite.Common;

public static partial class Tokenizer
{
    public static IEnumerable<string> Tokenize(string text)
    {
        string tokenString = text.ToLower();
        tokenString = RemoveNonAlphaNumericRegex().Replace(tokenString, " ");
        tokenString = TrimConsecutiveSpacesRegex().Replace(tokenString, " ");
        return tokenString.Split(' ').Except(Constants.StopWords).Where(a => a.Length > 0).ToArray();
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex RemoveNonAlphaNumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex TrimConsecutiveSpacesRegex();
}
