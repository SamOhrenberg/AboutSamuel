namespace PortfolioWebsite.Api.Dtos.Admin;

public class AdminRecruiterPostingDto
{
    public Guid RecruiterPostingId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? JobTitle { get; set; }
    public string? HiringCompany { get; set; }
    public string WorkArrangement { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public string ContractTerms { get; set; } = string.Empty;
    public double? AnnualPay { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = [];
    public List<string> Missing { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
    public bool CanReply { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public int EmailCount { get; set; }
    public List<string> Agencies { get; set; } = [];
    public bool Engaged { get; set; }  // already in conversation with someone about it
}

public class AdminRecruiterPostingDetailDto : AdminRecruiterPostingDto
{
    public List<AdminRecruiterPitchDto> Pitches { get; set; } = [];
}

public class AdminRecruiterPitchDto
{
    public string? RecruitingAgency { get; set; }
    public string? RecruiterName { get; set; }
    public double? AnnualPay { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? FromName { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string GmailUrl { get; set; } = string.Empty;
}

public class AdminRecruiterReviewRequest
{
    public string? ReviewStatus { get; set; }  // pending, interested, passed
    public string? Notes { get; set; }
}

public class AdminRecruiterSettingsDto
{
    public bool TriageEnabled { get; set; }
    public bool ShadowMode { get; set; }
    public double RemotePayFloor { get; set; }
    public double HybridPayFloor { get; set; }
    public double OnsitePayFloor { get; set; }
    public double HoursPerYear { get; set; }
    public bool AllowRemote { get; set; }
    public bool AllowHybrid { get; set; }
    public bool AllowOnsite { get; set; }
    public List<string> AcceptableLocations { get; set; } = [];
    public string HomeState { get; set; } = string.Empty;
    public List<string> AllowedEmploymentTypes { get; set; } = [];
    public List<string> DealbreakerContractTerms { get; set; } = [];
    public string FreeTextRequirements { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAt { get; set; }
}

public class AdminRecruiterStatsDto
{
    public Dictionary<string, int> ByReviewStatus { get; set; } = [];
    public Dictionary<string, int> ByOutcome { get; set; } = [];
    public DateTimeOffset? LastSyncedAt { get; set; }
    public bool TriageEnabled { get; set; }
    public bool ShadowMode { get; set; }
}
