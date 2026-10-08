using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services;
using static PortfolioWebsite.Api.Services.AgentServiceClient;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>
/// Recruiter triage review. The agent service reads Gmail and fills these tables; this
/// controller is the human-in-the-loop side: the review queue, Samuel's decisions, and
/// the preferences triage uses.
/// </summary>
[ApiController]
[Route("admin/recruiters")]
[Authorize(Roles = "Admin")]
public class AdminRecruiterController(
    ILogger<AdminRecruiterController> _logger, SqlDbContext _db, AgentServiceClient _agentClient) : ControllerBase
{
    private static readonly HashSet<string> ReviewStatuses = ["pending", "interested", "passed"];
    private static readonly HashSet<string> EmploymentTypes = ["full_time", "contract", "contract_to_hire", "part_time"];
    private static readonly HashSet<string> ContractTerms = ["w2", "c2c", "1099"];

    [HttpGet("stats")]
    public async Task<AdminRecruiterStatsDto> GetStats()
    {
        var settings = await Settings();
        return new AdminRecruiterStatsDto
        {
            ByReviewStatus = await _db.RecruiterPostings.GroupBy(p => p.ReviewStatus)
                .ToDictionaryAsync(g => g.Key, g => g.Count()),
            ByOutcome = await _db.RecruiterPostings.GroupBy(p => p.Outcome)
                .ToDictionaryAsync(g => g.Key, g => g.Count()),
            LastSyncedAt = settings.LastSyncedAt,
            TriageEnabled = settings.TriageEnabled,
            ShadowMode = settings.ShadowMode,
        };
    }

    /// <summary>The queue. Needs-review first, then most recent.</summary>
    [HttpGet("postings")]
    public async Task<IEnumerable<AdminRecruiterPostingDto>> GetPostings(
        [FromQuery] string? reviewStatus = null, [FromQuery] string? outcome = null, [FromQuery] int take = 100)
    {
        var query = _db.RecruiterPostings.AsNoTracking()
            .Include(p => p.Pitches).ThenInclude(x => x.Email)
            .AsQueryable();
        if (!string.IsNullOrEmpty(reviewStatus)) query = query.Where(p => p.ReviewStatus == reviewStatus);
        if (!string.IsNullOrEmpty(outcome)) query = query.Where(p => p.Outcome == outcome);

        var postings = await query
            .OrderBy(p => p.ReviewStatus == "pending" ? 0 : 1)
            .ThenByDescending(p => p.LastSeenAt)
            .Take(Math.Clamp(take, 1, 500))
            .AsSplitQuery()
            .ToListAsync();

        return postings.Select(p => Fill(new AdminRecruiterPostingDto(), p));
    }

    [HttpGet("postings/{id:guid}")]
    public async Task<IActionResult> GetPosting(Guid id)
    {
        var posting = await _db.RecruiterPostings.AsNoTracking()
            .Include(p => p.Pitches).ThenInclude(x => x.Email)
            .FirstOrDefaultAsync(p => p.RecruiterPostingId == id);
        if (posting is null) return NotFound();

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
        return Ok(detail);
    }

    /// <summary>Samuel's call on a posting: interested, passed, or back to pending. And notes.</summary>
    [HttpPatch("postings/{id:guid}")]
    public async Task<IActionResult> ReviewPosting(Guid id, [FromBody] AdminRecruiterReviewRequest request)
    {
        var posting = await _db.RecruiterPostings.FindAsync(id);
        if (posting is null) return NotFound();

        if (request.ReviewStatus is not null)
        {
            if (!ReviewStatuses.Contains(request.ReviewStatus))
                return BadRequest(new { Message = "reviewStatus must be pending, interested, or passed." });
            posting.ReviewStatus = request.ReviewStatus;
            posting.ReviewedAt = request.ReviewStatus == "pending" ? null : DateTimeOffset.UtcNow;
        }
        if (request.Notes is not null)
            posting.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        await _db.SaveChangesAsync();
        _logger.LogInformation("Recruiter posting {Id} reviewed: {Status}", id, posting.ReviewStatus);
        return NoContent();
    }

    public record MergeRequest(List<Guid> PostingIds);

    /// <summary>
    /// Samuel marking postings as the same job. The oldest one is kept, so its id (and any
    /// links to it) survive.
    /// </summary>
    [HttpPost("postings/merge")]
    public async Task<IActionResult> MergePostings([FromBody] MergeRequest request, CancellationToken ct)
    {
        var ids = request.PostingIds.Distinct().ToList();
        if (ids.Count is < 2 or > 50)
            return BadRequest(new { Message = "Pick between 2 and 50 postings." });

        var found = await _db.RecruiterPostings.AsNoTracking()
            .Where(p => ids.Contains(p.RecruiterPostingId))
            .OrderBy(p => p.FirstSeenAt)
            .Select(p => p.RecruiterPostingId)
            .ToListAsync(ct);
        if (found.Count != ids.Count) return NotFound(new { Message = "One of those postings no longer exists." });

        var target = found[0];
        var outcome = await _agentClient.MergeRecruiterPostingsAsync(target, found.Skip(1).ToList(), ct);
        _logger.LogInformation("Recruiter postings merge of {Count}: {Outcome}", ids.Count, outcome);
        return outcome switch
        {
            MergeOutcome.Merged => Ok(new { recruiterPostingId = target }),
            MergeOutcome.Busy => Conflict(new { Message = "Triage is running right now. Try again in a minute." }),
            MergeOutcome.NotFound => NotFound(new { Message = "One of those postings no longer exists." }),
            _ => StatusCode(502, new { Message = "The agent service couldn't merge them." }),
        };
    }

    [HttpGet("settings")]
    public async Task<AdminRecruiterSettingsDto> GetSettings() => ToDto(await Settings());

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] AdminRecruiterSettingsDto dto)
    {
        if (dto.RemotePayFloor < 0 || dto.HybridPayFloor < 0 || dto.OnsitePayFloor < 0)
            return BadRequest(new { Message = "Pay floors can't be negative." });
        if (dto.HoursPerYear is < 1 or > 8784)
            return BadRequest(new { Message = "Hours per year has to be between 1 and 8,784." });
        if (dto.AllowedEmploymentTypes.Count == 0 || !dto.AllowedEmploymentTypes.All(EmploymentTypes.Contains))
            return BadRequest(new { Message = "Pick at least one employment type." });
        if (!dto.DealbreakerContractTerms.All(ContractTerms.Contains))
            return BadRequest(new { Message = "Unknown contract term." });

        var settings = await _db.RecruiterTriageSettings.FirstAsync(s => s.RecruiterTriageSettingsId == 1);
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

        await _db.SaveChangesAsync();
        _logger.LogInformation("Recruiter triage settings saved (enabled {Enabled}, shadow {Shadow})",
            settings.TriageEnabled, settings.ShadowMode);
        return Ok(ToDto(settings));
    }

    // ---- helpers -------------------------------------------------------------

    private Task<RecruiterTriageSettings> Settings() =>
        _db.RecruiterTriageSettings.AsNoTracking().FirstAsync(s => s.RecruiterTriageSettingsId == 1);

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
