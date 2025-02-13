
namespace PortfolioWebsite.Common;

public static class Constants
{
    public static readonly string[] StopWords =
    [
        "i", "me", "my", "myself", "we", "our", "ours", "ourselves", "you", "your", "yours", "yourself", "yourselves",
        "he", "him", "his", "himself", "she", "her", "hers", "herself", "it", "its", "itself", "they", "them", "their",
        "theirs", "themselves", "what", "which", "who", "whom", "this", "that", "these", "those", "am", "is", "are", "was",
        "were", "be", "been", "being", "have", "has", "had", "having", "do", "does", "did", "doing", "a", "an", "the",
        "and", "but", "if", "or", "because", "as", "until", "while", "of", "at", "by", "for", "with", "about", "against",
        "between", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up", "down", "in", "out",
        "on", "off", "over", "under", "again", "further", "then", "once", "here", "there", "when", "where", "why", "how",
        "all", "any", "both", "each", "few", "more", "most", "other", "some", "such", "no", "nor", "not", "only", "own",
        "same", "so", "than", "too", "very", "s", "t", "can", "will", "just", "don", "should", "now"
    ];

    public static readonly object[] SupportedTools =
    [
        new
        {
            type = "function",
            function = new
            {
                name = "contactSamuel",
                description = "Accepts the email of the user and an optional message from the user that generates a contact request for Samuel Ohrenberg. This allows users to contact Samuel in case they have further questions or wish to discuss something",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        email = new
                        {
                            type = "string",
                            description = "The user's email address that Samuel will use to contact them"
                        },
                        message = new
                        {
                            type = "string",
                            description = "An optional short message from the user explaining what the contact request is for."
                        }
                    },
                    required = new [] { "email" }
                }
            }
        },
        new 
        {
            type = "function",
            function = new
            {
                name = "redirectToPage",
                description = "Checks if the user is wanting information that can be found on one of the following pages: 'Contact', 'Work History', 'Projects', 'Education'",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        page = new
                        {
                            type = "string",
                            description = "The page that the user is wanting to be redirected to"
                        }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "getResume",
                description = "Returns the resume of Samuel Ohrenberg in PDF format"
            }
        },
        new 
        {
            type = "function",
            function = new
            {
                name = "askQuestion",
                description = "Accepts a technical or interview question from the user and returns an answer",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        question = new
                        {
                            type = "string",
                            description = "The question that the user is asking"
                        }
                    }
                }
            }
        }
    ];

}
