namespace PortfolioWebsite.Api.Data.Models;

public class AdversarialCaseResult
{
    public Guid AdversarialCaseResultId { get; set; }
    public Guid AdversarialRunId { get; set; }

    // The case
    public string CaseKey { get; set; } = string.Empty;   // fp-01, gen-twisted_fact-2...
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;    // suite, generated
    public string Prompt { get; set; } = string.Empty;
    public string History { get; set; } = "[]";           // JSON, earlier turns
    public string? PlantedClaim { get; set; }

    // What SamuelLM did
    public string Answer { get; set; } = string.Empty;
    public string ToolCalls { get; set; } = "[]";         // JSON
    public bool BlockedByContentFilter { get; set; }
    public int DurationMs { get; set; }

    // The judge's verdict
    public string Verdict { get; set; } = string.Empty;   // pass, fail, invalid_case, error
    public string FailureType { get; set; } = "none";
    public string Severity { get; set; } = "none";
    public string PremiseHandling { get; set; } = "not_applicable";
    public string Claims { get; set; } = "[]";            // JSON: the judge's claim checklist
    public string Explanation { get; set; } = string.Empty;
}
