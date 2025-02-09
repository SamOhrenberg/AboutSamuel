using System.Text;
using System.Text.RegularExpressions;

namespace PortfolioWebsite.Common;

public static partial class Tokenizer
{
    private static readonly Dictionary<string, string> technologyPlaceholders = new()
    {
        { "c#", "TECHCSHARP" },
        { ".net", "TECHDOTNET" },
        { "asp.net", "TECHASPNET" },
        { "vue.js", "TECHVUEJS" },
        { "react.js", "TECHREACTJS" },
        { "angular.js", "TECHANGULARJS" },
        { "node.js", "TECHNODEJS" },
        { "backbone.js", "TECHBACKBONEJS" },
        { "ember.js", "TECHEMBERJS" },
        { "gitlab ci", "TECHGITLABCI" },
        { "github actions", "TECHGITHUBACTIONS" },
        { "bitbucket pipelines", "TECHBITBUCKETPIPELINES" }
    };

    public static IEnumerable<string> Tokenize(string text)
    {
        string tokenString = text.ToLower();

        // Replace technology names with placeholders
        foreach (var tech in technologyPlaceholders)
        {
            tokenString = tokenString.Replace(tech.Key, tech.Value);
        }

        tokenString = RemoveNonAlphaNumericRegex().Replace(tokenString, "");
        tokenString = TrimConsecutiveSpacesRegex().Replace(tokenString, " ");

        var tokens = tokenString.Split(' ').Except(Constants.StopWords).Where(a => a.Length > 0).ToArray();

        // Replace placeholders back with original technology names
        for (int i = 0; i < tokens.Length; i++)
        {
            foreach (var tech in technologyPlaceholders)
            {
                if (tokens[i].Equals(tech.Value, StringComparison.CurrentCultureIgnoreCase))
                {
                    tokens[i] = tech.Key;
                }
            }
        }

        return tokens;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9 ]")]
    private static partial Regex RemoveNonAlphaNumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex TrimConsecutiveSpacesRegex();
}