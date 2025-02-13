namespace PortfolioWebsite.Api.Services.Entities;

public class ChatResponse
{
    public string Message { get; set; }
    public bool Error { get; set;  }
    public bool TokenLimitReached { get; set;  }
    public bool ReturnResume { get; set;  }
    public string? RedirectToPage { get; set; }
    public IEnumerable<ToolCall>? ToolCalls { get; set; }

    public ChatResponse(string message, bool error, bool tokenLimitReached = false, bool returnResume = false, string? redirectToPage = null, IEnumerable<ToolCall>? toolCalls = null)
    {
        Message = message;
        Error = error;
        TokenLimitReached = tokenLimitReached;
        ReturnResume = returnResume;
        RedirectToPage = redirectToPage;
        ToolCalls = toolCalls;
    }
}
