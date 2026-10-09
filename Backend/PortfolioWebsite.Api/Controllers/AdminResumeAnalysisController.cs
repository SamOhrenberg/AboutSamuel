using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Services;
using static PortfolioWebsite.Api.Services.ResumeSuggestionApplier;

namespace PortfolioWebsite.Api.Controllers;

public record AnalyzeRequest(Guid? ResumeFileId);

public record ApproveSuggestionRequest(Dictionary<string, JsonElement>? Overrides);

public record ResumeAnalysisDto(
    Guid ResumeAnalysisId, Guid ResumeFileId, string ResumeFileName, string Status, string Model, string? Summary,
    string? Error, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int Pending, int Applied, int Rejected);

public record ResumeSuggestionDto(
    Guid ResumeSuggestionId, string Action, string EntityType, Guid? EntityId, string Label, JsonElement Changes,
    string Rationale, string Evidence, string Status, DateTimeOffset? DecidedAt);

public record ResumeAnalysisDetailDto(ResumeAnalysisDto Analysis, IEnumerable<ResumeSuggestionDto> Suggestions);

/// <summary>
/// The review side of resume analysis. The agent service reads the uploaded resume, compares it
/// with the site data, and saves suggestions as pending. Nothing reaches the site until a
/// suggestion is approved here.
/// </summary>
[ApiController]
[Route("admin/resume")]
[Authorize(Roles = "Admin")]
public class AdminResumeAnalysisController(
    ILogger<AdminResumeAnalysisController> _logger,
    SqlDbContext _db,
    AgentServiceClient _agentClient,
    ResumeSuggestionApplier _applier) : ControllerBase
{
    /// <summary>Starts an analysis and streams its progress through to the admin page.</summary>
    [HttpPost("analyze")]
    public async Task Analyze([FromBody] AnalyzeRequest? request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        _logger.LogInformation("Admin started a resume analysis");
        try
        {
            await foreach (var payload in _agentClient.StreamResumeAnalysisAsync(request?.ResumeFileId, ct))
            {
                await Response.WriteAsync($"data: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The admin closed the page. Cancelling also stopped the agent service call.
        }
    }

    [HttpGet("analyses")]
    public async Task<IEnumerable<ResumeAnalysisDto>> GetAnalyses([FromQuery] int take = 10)
    {
        var analyses = await _db.ResumeAnalyses.AsNoTracking()
            .Include(a => a.ResumeFile)
            .Include(a => a.Suggestions)
            .OrderByDescending(a => a.StartedAt)
            .Take(Math.Clamp(take, 1, 50))
            .AsSplitQuery()
            .ToListAsync();
        return analyses.Select(ToDto);
    }

    [HttpGet("analyses/{id:guid}")]
    public async Task<ActionResult<ResumeAnalysisDetailDto>> GetAnalysis(Guid id)
    {
        var analysis = await _db.ResumeAnalyses.AsNoTracking()
            .Include(a => a.ResumeFile)
            .Include(a => a.Suggestions)
            .FirstOrDefaultAsync(a => a.ResumeAnalysisId == id);
        if (analysis is null) return NotFound();

        return new ResumeAnalysisDetailDto(ToDto(analysis), analysis.Suggestions
            .OrderBy(s => s.Status == "pending" ? 0 : 1)
            .ThenBy(s => s.DisplayOrder)
            .Select(ToDto));
    }

    /// <summary>Applies a suggestion to the site data, optionally with edited values.</summary>
    [HttpPost("suggestions/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSuggestionRequest? request, CancellationToken ct)
    {
        var result = await _applier.ApproveAsync(id, request?.Overrides, ct);
        return result.Outcome switch
        {
            Outcome.Applied => Ok(new { Message = "Applied.", result.EmbeddingGenerated }),
            Outcome.NotFound => NotFound(),
            Outcome.Conflict => Conflict(new { Message = result.Message }),
            _ => BadRequest(new { Message = result.Message }),
        };
    }

    [HttpPost("suggestions/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var result = await _applier.RejectAsync(id, ct);
        return result.Outcome switch
        {
            Outcome.Applied => NoContent(),
            Outcome.NotFound => NotFound(),
            _ => Conflict(new { Message = result.Message }),
        };
    }

    private static ResumeAnalysisDto ToDto(ResumeAnalysis a) => new(
        a.ResumeAnalysisId, a.ResumeFileId, a.ResumeFile.FileName, a.Status, a.Model, a.Summary, a.Error,
        a.StartedAt, a.CompletedAt,
        a.Suggestions.Count(s => s.Status == "pending"),
        a.Suggestions.Count(s => s.Status == "applied"),
        a.Suggestions.Count(s => s.Status == "rejected"));

    private static ResumeSuggestionDto ToDto(ResumeSuggestion s) => new(
        s.ResumeSuggestionId, s.Action, s.EntityType, s.EntityId, s.Label,
        JsonSerializer.Deserialize<JsonElement>(s.Changes), s.Rationale, s.Evidence, s.Status, s.DecidedAt);
}
