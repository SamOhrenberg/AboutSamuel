using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;
using static PortfolioWebsite.Api.Services.AgentServiceClient;

namespace PortfolioWebsite.Api.Services.Admin;

/// <summary>
/// Recruiter triage review. The agent service reads Gmail and fills these tables; this is
/// the human-in-the-loop side: the review queue, Samuel's decisions, and the preferences
/// triage uses.
/// </summary>
public class AdminRecruiterService(SqlDbContext _db, AgentServiceClient _agentClient, ILogger<AdminRecruiterService> _logger)
{
    public static readonly string[] ReviewStatuses = ["pending", "interested", "passed"];
    public static readonly string[] EmploymentTypes = ["full_time", "contract", "contract_to_hire", "part_time"];
    public static readonly string[] ContractTermValues = ["w2", "c2c", "1099"];
    public const int MaxPostings = 500;

    public async Task<AdminRecruiterStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var settings = await Settings(ct);
        return new AdminRecruiterStatsDto
        {
            ByReviewStatus = await _db.RecruiterPostings.GroupBy(p => p.ReviewStatus)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct),
            ByOutcome = await _db.RecruiterPostings.GroupBy(p => p.Outcome)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct),
            LastSyncedAt = settings.LastSyncedAt,
            TriageEnabled = settings.TriageEnabled,
            ShadowMode = settings.ShadowMode,
        };
    }

    /// <summary>The queue. Needs-review first, then most recent.</summary>
    public async Task<List<AdminRecruiterPostingDto>> ListPostingsAsync(
        string? reviewStatus, string? outcome, int take, CancellationToken ct = default)
    {
        var query = _db.RecruiterPostings.AsNoTracking()
            .Include(p => p.Pitches).ThenInclude(x => x.Email)
            .AsQueryable();
        if (!string.IsNullOrEmpty(reviewStatus)) query = query.Where(p => p.ReviewStatus == reviewStatus);
        if (!string.IsNullOrEmpty(outcome)) query = query.Where(p => p.Outcome == outcome);

        var postings = await query
            .OrderBy(p => p.ReviewStatus == "pending" ? 0 : 1)
            .ThenByDescending(p => p.LastSeenAt)
            .Take(Math.Clamp(take, 1, MaxPostings))
            .AsSplitQuery()
            .ToListAsync(ct);

        return postings.Select(p => Fill(new AdminRecruiterPostingDto(), p)).ToList();
    }

    public async Task<AdminResult<AdminRecruiterPostingDetailDto>> GetPostingAsync(Guid id, CancellationToken ct = default)
    {
        var posting = await _db.RecruiterPostings.AsNoTracking()
            .Include(p => p.Pitches).ThenInclude(x => x.Email)
            .FirstOrDefaultAsync(p => p.RecruiterPostingId == id, ct);
        if (posting is null) return AdminFailure.NotFound();

        var detail = Fill(new AdminRecruiterPostingDetailDto(), posting);
        detail.Pitches = posting.Pitches
            .Where(x => x.Email is not null)
            .OrderByDescending(x => x.Email!.ReceivedAt)
            .Select(x => new AdminRecruiterPitchDto
            {
                RecruitingAgency = x.RecruitingAgency,
                RecruiterName = x.RecruiterName,
                AnnualPay = x.AnnualPay,
                ReceivedAt = x.Email!.ReceivedAt,
                FromName = x.Email.FromName,
                FromAddress = x.Email.FromAddress,
                Subject = x.Email.Subject,
                Snippet = x.Email.Snippet,
                Category = x.Email.Category,
                Action = x.Email.Action,
                // Gmail opens a conversation by its thread id
                GmailUrl = $"https://mail.google.com/mail/u/0/#all/{x.Email.GmailThreadId}",
            })
            .ToList();
        return detail;
    }

    /// <summary>Samuel's call on a posting (interested, passed, or back to pending) and notes.
    /// Null leaves a field as it is; blank notes clear them.</summary>
    public async Task<AdminResult> ReviewPostingAsync(Guid id, AdminRecruiterReviewRequest request, CancellationToken ct = default)
    {
        var posting = await _db.RecruiterPostings.FindAsync([id], ct);
        if (posting is null) return AdminFailure.NotFound();

        if (request.ReviewStatus is not null)
        {
            if (!ReviewStatuses.Contains(request.ReviewStatus))
                return AdminFailure.Invalid("reviewStatus must be pending, interested, or passed.");
            posting.ReviewStatus = request.ReviewStatus;
            posting.ReviewedAt = request.ReviewStatus == "pending" ? null : DateTimeOffset.UtcNow;
        }
        if (request.Notes is not null)
            posting.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Recruiter posting {Id} reviewed: {Status}", id, posting.ReviewStatus);
        return AdminResult.Ok();
    }

    /// <summary>
    /// Marks postings as the same job. The oldest one is kept, so its id (and any links to it)
    /// survive. Returns the kept posting's id.
    /// </summary>
    public async Task<AdminResult<Guid>> MergePostingsAsync(IEnumerable<Guid> postingIds, CancellationToken ct = default)
    {
        var ids = postingIds.Distinct().ToList();
        if (ids.Count is < 2 or > 50)
            return AdminFailure.Invalid("Pick between 2 and 50 postings.");

        var found = await _db.RecruiterPostings.AsNoTracking()
            .Where(p => ids.Contains(p.RecruiterPostingId))
            .OrderBy(p => p.FirstSeenAt)
            .Select(p => p.RecruiterPostingId)
            .ToListAsync(ct);
        if (found.Count != ids.Count) return AdminFailure.NotFound("One of those postings no longer exists.");

        var target = found[0];
        var outcome = await _agentClient.MergeRecruiterPostingsAsync(target, found.Skip(1).ToList(), ct);
        _logger.LogInformation("Recruiter postings merge of {Count}: {Outcome}", ids.Count, outcome);
        return outcome switch
        {
            MergeOutcome.Merged => target,
            MergeOutcome.Busy => AdminFailure.Conflict("Triage is running right now. Try again in a minute."),
            MergeOutcome.NotFound => AdminFailure.NotFound("One of those postings no longer exists."),
            _ => AdminFailure.Unavailable("The agent service couldn't merge them."),
        };
    }

    public async Task<AdminRecruiterSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        ToDto(await Settings(ct));

    public async Task<AdminResult<AdminRecruiterSettingsDto>> SaveSettingsAsync(AdminRecruiterSettingsDto dto,
        CancellationToken ct = default)
    {
        if (dto.RemotePayFloor < 0 || dto.HybridPayFloor < 0 || dto.OnsitePayFloor < 0)
            return AdminFailure.Invalid("Pay floors can't be negative.");
        if (dto.HoursPerYear is < 1 or > 8784)
            return AdminFailure.Invalid("Hours per year has to be between 1 and 8,784.");
        if (dto.AllowedEmploymentTypes.Count == 0 || !dto.AllowedEmploymentTypes.All(EmploymentTypes.Contains))
            return AdminFailure.Invalid("Pick at least one employment type.");
        if (!dto.DealbreakerContractTerms.All(ContractTermValues.Contains))
            return AdminFailure.Invalid("Unknown contract term.");

        var settings = await _db.RecruiterTriageSettings.FirstAsync(s => s.RecruiterTriageSettingsId == 1, ct);
        settings.TriageEnabled = dto.TriageEnabled;
        settings.ShadowMode = dto.ShadowMode;
        settings.RemotePayFloor = dto.RemotePayFloor;
        settings.HybridPayFloor = dto.HybridPayFloor;
        settings.OnsitePayFloor = dto.OnsitePayFloor;
        settings.HoursPerYear = dto.HoursPerYear;
        settings.AllowRemote = dto.AllowRemote;
        settings.AllowHybrid = dto.AllowHybrid;
        settings.AllowOnsite = dto.AllowOnsite;
        settings.AcceptableLocations = JsonSerializer.Serialize(
            dto.AcceptableLocations.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
        settings.HomeState = dto.HomeState.Trim().ToUpperInvariant();
        settings.AllowedEmploymentTypes = JsonSerializer.Serialize(dto.AllowedEmploymentTypes.Distinct());
        settings.DealbreakerContractTerms = JsonSerializer.Serialize(dto.DealbreakerContractTerms.Distinct());
        settings.FreeTextRequirements = dto.FreeTextRequirements.Trim();
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Recruiter triage settings saved (enabled {Enabled}, shadow {Shadow})",
            settings.TriageEnabled, settings.ShadowMode);
        return ToDto(settings);
    }

    private Task<RecruiterTriageSettings> Settings(CancellationToken ct) =>
        _db.RecruiterTriageSettings.AsNoTracking().FirstAsync(s => s.RecruiterTriageSettingsId == 1, ct);

    private static T Fill<T>(T dto, RecruiterPosting p) where T : AdminRecruiterPostingDto
    {
        dto.RecruiterPostingId = p.RecruiterPostingId;
        dto.FirstSeenAt = p.FirstSeenAt;
        dto.LastSeenAt = p.LastSeenAt;
        dto.JobTitle = p.JobTitle;
        dto.HiringCompany = p.HiringCompany;
        dto.WorkArrangement = p.WorkArrangement;
        dto.Location = p.Location;
        dto.EmploymentType = p.EmploymentType;
        dto.ContractTerms = p.ContractTerms;
        dto.AnnualPay = p.AnnualPay;
        dto.Outcome = p.Outcome;
        dto.Reasons = List(p.Reasons);
        dto.Missing = List(p.Missing);
        dto.Conflicts = List(p.Conflicts);
        dto.CanReply = p.CanReply;
        dto.ReviewStatus = p.ReviewStatus;
        dto.Notes = p.Notes;
        dto.ReviewedAt = p.ReviewedAt;
        dto.EmailCount = p.Pitches.Select(x => x.RecruiterEmailId).Distinct().Count();
        dto.Agencies = p.Pitches.Select(x => x.RecruitingAgency).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        dto.Engaged = p.Pitches.Any(x => x.Email is not null && (x.Email.Category == "application_update"
            || x.Email.Subject.StartsWith("Message replied", StringComparison.OrdinalIgnoreCase)));
        return dto;
    }

    private static AdminRecruiterSettingsDto ToDto(RecruiterTriageSettings s) => new()
    {
        TriageEnabled = s.TriageEnabled,
        ShadowMode = s.ShadowMode,
        RemotePayFloor = s.RemotePayFloor,
        HybridPayFloor = s.HybridPayFloor,
        OnsitePayFloor = s.OnsitePayFloor,
        HoursPerYear = s.HoursPerYear,
        AllowRemote = s.AllowRemote,
        AllowHybrid = s.AllowHybrid,
        AllowOnsite = s.AllowOnsite,
        AcceptableLocations = List(s.AcceptableLocations),
        HomeState = s.HomeState,
        AllowedEmploymentTypes = List(s.AllowedEmploymentTypes),
        DealbreakerContractTerms = List(s.DealbreakerContractTerms),
        FreeTextRequirements = s.FreeTextRequirements,
        LastSyncedAt = s.LastSyncedAt,
    };

    private static List<string> List(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
