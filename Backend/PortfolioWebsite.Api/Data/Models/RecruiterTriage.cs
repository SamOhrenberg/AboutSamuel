using Pgvector;

namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// Recruiter triage: the agent service reads Samuel's Gmail, classifies recruiter outreach,
/// groups duplicate postings, and decides what to do. These tables are written by the agent
/// service (schema lives here so EF migrations own the database) and read and edited by
/// the admin panel. Only short snippets of emails are stored, never full bodies.
/// </summary>
public class RecruiterTriageSettings
{
    public int RecruiterTriageSettingsId { get; set; }  // always 1: there's one row

    // Switches. Triage starts off, and in shadow mode it only labels and drafts, never sends.
    public bool TriageEnabled { get; set; }
    public bool ShadowMode { get; set; } = true;

    // Preferences. Annual pay floors; hourly pay is annualized at HoursPerYear.
    public double RemotePayFloor { get; set; } = 90_000;
    public double HybridPayFloor { get; set; } = 90_000;
    public double OnsitePayFloor { get; set; } = 90_000;
    public double HoursPerYear { get; set; } = 2080;
    public bool AllowRemote { get; set; } = true;
    public bool AllowHybrid { get; set; } = true;
    public bool AllowOnsite { get; set; } = true;
    public string AcceptableLocations { get; set; } = "[]";       // JSON array of towns
    public string HomeState { get; set; } = "OK";
    public string AllowedEmploymentTypes { get; set; } = "[]";    // JSON: full_time, contract_to_hire...
    public string DealbreakerContractTerms { get; set; } = "[]";  // JSON: c2c, 1099
    public string FreeTextRequirements { get; set; } = string.Empty;

    // Sync bookkeeping, owned by the agent service
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One real job opening, however many recruiters pitched it.</summary>
public class RecruiterPosting
{
    public Guid RecruiterPostingId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    // Best known details, merged across every email that pitched it
    public string? JobTitle { get; set; }
    public string? HiringCompany { get; set; }
    public string WorkArrangement { get; set; } = "unknown";      // remote, hybrid, onsite, unknown
    public string? Location { get; set; }
    public string EmploymentType { get; set; } = "unknown";       // full_time, contract, contract_to_hire...
    public string ContractTerms { get; set; } = "unknown";        // w2, c2c, 1099, unknown
    public double? AnnualPay { get; set; }                        // highest quoted, annualized
    public string Role { get; set; } = "{}";                      // JSON: the full merged role details

    // Matching future emails against this posting
    public Vector? Embedding { get; set; }

    // What triage decided, and why
    public string Outcome { get; set; } = string.Empty;           // review, ask_info, decline
    public string Reasons { get; set; } = "[]";                   // JSON array of readable reasons
    public string Missing { get; set; } = "[]";                   // JSON: what to ask the recruiter for
    public string Conflicts { get; set; } = "[]";                 // JSON: where agencies disagree
    public bool CanReply { get; set; } = true;

    // Samuel's review
    public string ReviewStatus { get; set; } = "pending";         // pending, interested, passed, auto
    public string? Notes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public virtual ICollection<RecruiterPitch> Pitches { get; set; } = [];
}

/// <summary>One Gmail message triage has looked at, recruiter or not, so it's never processed twice.</summary>
public class RecruiterEmail
{
    public Guid RecruiterEmailId { get; set; }
    public string GmailMessageId { get; set; } = string.Empty;
    public string GmailThreadId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }

    public string? FromName { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;           // a few hundred characters, not the body

    public string Category { get; set; } = string.Empty;          // recruiter_outreach, job_board_or_newsletter...
    public bool Personalized { get; set; }
    public string Confidence { get; set; } = string.Empty;

    // What triage did in Gmail
    public string Action { get; set; } = "none";                   // none, labeled, drafted, sent
    public string? GmailDraftId { get; set; }

    public virtual ICollection<RecruiterPitch> Pitches { get; set; } = [];
}

/// <summary>One role pitched in one email: which agency, and what it quoted for that posting.</summary>
public class RecruiterPitch
{
    public Guid RecruiterPitchId { get; set; }
    public Guid RecruiterEmailId { get; set; }
    public Guid RecruiterPostingId { get; set; }

    public string? RecruitingAgency { get; set; }
    public string? RecruiterName { get; set; }
    public double? AnnualPay { get; set; }                        // this agency's quote, annualized
    public string Role { get; set; } = "{}";                      // JSON: the role as this email described it

    public virtual RecruiterEmail? Email { get; set; }
    public virtual RecruiterPosting? Posting { get; set; }
}
