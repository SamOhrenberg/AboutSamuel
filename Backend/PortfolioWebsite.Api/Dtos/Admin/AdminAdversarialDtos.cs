using System.Text.Json;

namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminAdversarialRunDto
{
    public Guid AdversarialRunId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string TargetModel { get; set; } = string.Empty;
    public string JudgeModel { get; set; } = string.Empty;
    public int TotalCases { get; set; }
    public int CasesAnswered { get; set; }
    public int CasesJudged { get; set; }
    public int Passed { get; set; }
    public int Scored { get; set; }
    public double? PassRate { get; set; }
    public JsonElement? Summary { get; set; }  // per-category counts
}

public class AdminAdversarialRunDetailDto : AdminAdversarialRunDto
{
    public List<AdminAdversarialCaseDto> Cases { get; set; } = [];
}

public class AdminAdversarialCaseDto
{
    public string CaseKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public JsonElement History { get; set; }
    public string? PlantedClaim { get; set; }
    public string Answer { get; set; } = string.Empty;
    public JsonElement ToolCalls { get; set; }
    public bool BlockedByContentFilter { get; set; }
    public int DurationMs { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string FailureType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string PremiseHandling { get; set; } = string.Empty;
    public JsonElement Claims { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
