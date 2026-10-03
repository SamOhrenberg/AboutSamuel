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
/// Adversarial test runs against SamuelLM. The agent service does the work and writes
/// the results; this controller starts runs and reads them back for the admin panel.
/// </summary>
[ApiController]
[Route("admin/adversarial")]
[Authorize(Roles = "Admin")]
public class AdminAdversarialController(
    ILogger<AdminAdversarialController> _logger,
    SqlDbContext _db,
    AgentServiceClient _agentClient) : ControllerBase
{
    private static readonly Dictionary<string, int> SeverityOrder =
        new() { ["high"] = 0, ["medium"] = 1, ["low"] = 2, ["none"] = 3 };

    [HttpPost("runs")]
    public async Task<IActionResult> StartRun(CancellationToken ct)
    {
        var (outcome, runId) = await _agentClient.StartAdversarialRunAsync(ct);
        _logger.LogInformation("Admin started adversarial run: {Outcome} {RunId}", outcome, runId);

        return outcome switch
        {
            StartRunOutcome.Started => Accepted(new { runId }),
            StartRunOutcome.AlreadyRunning => Conflict(new { Message = "A run is already in progress." }),
            _ => StatusCode(StatusCodes.Status502BadGateway,
                    new { Message = "Couldn't reach the agent service to start a run." }),
        };
    }

    [HttpGet("runs")]
    public async Task<IEnumerable<AdminAdversarialRunDto>> GetRuns([FromQuery] int take = 20)
    {
        var runs = await _db.AdversarialRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync();

        return runs.Select(r => Fill(new AdminAdversarialRunDto(), r));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id)
    {
        var run = await _db.AdversarialRuns
            .AsNoTracking()
            .Include(r => r.Cases)
            .FirstOrDefaultAsync(r => r.AdversarialRunId == id);
        if (run is null) return NotFound();

        var detail = Fill(new AdminAdversarialRunDetailDto(), run);

        // Failures first, worst severity at the top, then everything else by case id
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

        return Ok(detail);
    }

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
    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        return doc.RootElement.Clone();
    }
}
