using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;
using static PortfolioWebsite.Api.Services.AgentServiceClient;

namespace PortfolioWebsite.Api.Services.Admin;

public record AdminChatStatsDto(int TotalMessages, int TotalSessions, int ErrorCount, int MessagesToday, double AvgResponseMs);

public record AdminJobFitRunDto(
    Guid JobFitRunId, DateTimeOffset ReceivedAt, float ResponseTookMs, Guid? SessionTrackingId, string? JobTitle,
    string? Company, bool IsJobDescription, string? Rating, double? FitScore, string? MustHavesMet, string? Error);

public record AdminJobFitRequirementDto(
    string RequirementKey, string Text, string Category, string Importance, string? Status, string? Reason,
    JsonElement Citations);

public record AdminJobFitRunDetailDto(
    AdminJobFitRunDto Run, string JobDescription, JsonElement? Summary, string? CoverLetter,
    List<AdminJobFitRequirementDto> Requirements);

/// <summary>
/// What visitors and the agent service have done: chat logs, adversarial test runs and Job
/// Fit runs. Mostly reads; starting an adversarial run is the one action.
/// </summary>
public class AdminActivityService(SqlDbContext _db, AgentServiceClient _agentClient, ILogger<AdminActivityService> _logger)
{
    private static readonly Dictionary<string, int> SeverityOrder =
        new() { ["high"] = 0, ["medium"] = 1, ["low"] = 2, ["none"] = 3 };

    /// <summary>Chat messages grouped by SessionTrackingId, most recently active session first.</summary>
    public async Task<List<AdminChatSessionDto>> ListChatSessionsAsync(int page, int pageSize, bool errorsOnly,
        CancellationToken ct = default)
    {
        var query = _db.Chats.AsQueryable();
        if (errorsOnly)
            query = query.Where(c => c.Error);

        var chats = await query.OrderBy(c => c.ReceivedAt).ToListAsync(ct);

        // Grouped in memory by session; sessions without a tracking id share the null group
        return chats
            .GroupBy(c => c.SessionTrackingId)
            .Select(g => new AdminChatSessionDto
            {
                SessionTrackingId = g.Key,
                FirstMessageAt = g.Min(c => c.ReceivedAt),
                LastMessageAt = g.Max(c => c.ReceivedAt),
                MessageCount = g.Count(),
                HadError = g.Any(c => c.Error),
                HadTokenLimit = g.Any(c => c.TokenLimitReached),
                Messages = g.OrderBy(c => c.ReceivedAt)
                    .Select(c => new AdminChatMessageDto
                    {
                        ChatId = c.ChatId,
                        Message = c.Message,
                        Response = c.Response,
                        ReceivedAt = c.ReceivedAt,
                        ResponseTookMs = c.ResponseTookMs,
                        Error = c.Error,
                        TokenLimitReached = c.TokenLimitReached,
                    })
                    .ToList(),
            })
            .OrderByDescending(s => s.LastMessageAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<AdminChatStatsDto> GetChatStatsAsync(CancellationToken ct = default)
    {
        var total = await _db.Chats.CountAsync(ct);
        var errors = await _db.Chats.CountAsync(c => c.Error, ct);
        var sessions = await _db.Chats.Select(c => c.SessionTrackingId).Distinct().CountAsync(ct);
        var today = await _db.Chats.CountAsync(c => c.ReceivedAt >= DateTimeOffset.UtcNow.Date, ct);
        var avgMs = total == 0 ? 0 : await _db.Chats.AverageAsync(c => (double)c.ResponseTookMs, ct);

        return new AdminChatStatsDto(total, sessions, errors, today, Math.Round(avgMs, 1));
    }

    /// <summary>Asks the agent service to start a run. Returns the new run's id.</summary>
    public async Task<AdminResult<Guid>> StartAdversarialRunAsync(CancellationToken ct = default)
    {
        var (outcome, runId) = await _agentClient.StartAdversarialRunAsync(ct);
        _logger.LogInformation("Admin started adversarial run: {Outcome} {RunId}", outcome, runId);

        return outcome switch
        {
            StartRunOutcome.Started => runId!.Value,
            StartRunOutcome.AlreadyRunning => AdminFailure.Conflict("A run is already in progress."),
            _ => AdminFailure.Unavailable("Couldn't reach the agent service to start a run."),
        };
    }

    public async Task<List<AdminAdversarialRunDto>> ListAdversarialRunsAsync(int take, CancellationToken ct = default)
    {
        var runs = await _db.AdversarialRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        return runs.Select(r => Fill(new AdminAdversarialRunDto(), r)).ToList();
    }

    /// <summary>A run with every case: failures first, worst severity at the top, then by case key.</summary>
    public async Task<AdminResult<AdminAdversarialRunDetailDto>> GetAdversarialRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _db.AdversarialRuns
            .AsNoTracking()
            .Include(r => r.Cases)
            .FirstOrDefaultAsync(r => r.AdversarialRunId == id, ct);
        if (run is null) return AdminFailure.NotFound();

        var detail = Fill(new AdminAdversarialRunDetailDto(), run);
        detail.Cases = run.Cases
            .OrderBy(c => c.Verdict == "pass" ? 1 : 0)
            .ThenBy(c => SeverityOrder.GetValueOrDefault(c.Severity, 4))
            .ThenBy(c => c.CaseKey)
            .Select(c => new AdminAdversarialCaseDto
            {
                CaseKey = c.CaseKey,
                Category = c.Category,
                Source = c.Source,
                Prompt = c.Prompt,
                History = ParseJson(c.History),
                PlantedClaim = c.PlantedClaim,
                Answer = c.Answer,
                ToolCalls = ParseJson(c.ToolCalls),
                BlockedByContentFilter = c.BlockedByContentFilter,
                DurationMs = c.DurationMs,
                Verdict = c.Verdict,
                FailureType = c.FailureType,
                Severity = c.Severity,
                PremiseHandling = c.PremiseHandling,
                Claims = ParseJson(c.Claims),
                Explanation = c.Explanation,
            })
            .ToList();
        return detail;
    }

    public async Task<List<AdminJobFitRunDto>> ListJobFitRunsAsync(int take, bool jobDescriptionsOnly,
        CancellationToken ct = default)
    {
        var query = _db.JobFitRuns.AsNoTracking();
        if (jobDescriptionsOnly)
            query = query.Where(r => r.IsJobDescription);

        var runs = await query
            .OrderByDescending(r => r.ReceivedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        return runs.Select(ToDto).ToList();
    }

    public async Task<AdminResult<AdminJobFitRunDetailDto>> GetJobFitRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _db.JobFitRuns
            .AsNoTracking()
            .Include(r => r.Requirements)
            .FirstOrDefaultAsync(r => r.JobFitRunId == id, ct);
        if (run is null) return AdminFailure.NotFound();

        return new AdminJobFitRunDetailDto(
            ToDto(run),
            run.JobDescription,
            run.Summary is null ? null : ParseJson(run.Summary),
            run.CoverLetter,
            run.Requirements
                .OrderBy(r => r.Importance == "must_have" ? 0 : 1)
                .ThenBy(r => r.RequirementKey)
                .Select(r => new AdminJobFitRequirementDto(r.RequirementKey, r.Text, r.Category, r.Importance,
                    r.Status, r.Reason, ParseJson(r.Citations)))
                .ToList());
    }

    private static AdminJobFitRunDto ToDto(JobFitRun r) => new(
        r.JobFitRunId, r.ReceivedAt, r.ResponseTookMs, r.SessionTrackingId, r.JobTitle, r.Company,
        r.IsJobDescription, r.Rating, r.FitScore, r.MustHavesMet, r.Error);

    private static T Fill<T>(T dto, AdversarialRun r) where T : AdminAdversarialRunDto
    {
        dto.AdversarialRunId = r.AdversarialRunId;
        dto.StartedAt = r.StartedAt;
        dto.FinishedAt = r.FinishedAt;
        dto.Status = r.Status;
        dto.Error = r.Error;
        dto.TargetModel = r.TargetModel;
        dto.JudgeModel = r.JudgeModel;
        dto.TotalCases = r.TotalCases;
        dto.CasesAnswered = r.CasesAnswered;
        dto.CasesJudged = r.CasesJudged;
        dto.Passed = r.Passed;
        dto.Scored = r.Scored;
        dto.PassRate = r.PassRate;
        dto.Summary = r.Summary is null ? null : ParseJson(r.Summary);
        return dto;
    }

    // The agent service stores these as JSON text. Send them as real JSON, not strings.
    // Anything that isn't valid JSON comes through as a string rather than failing the read.
    private static JsonElement ParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(json);
        }
    }
}
