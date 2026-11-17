using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>
/// The review side of resume analysis. The agent service reads the uploaded resume, compares it
/// with the site data, and saves suggestions as pending. Nothing reaches the site until a
/// suggestion is approved here.
/// </summary>
[ApiController]
[Route("admin/resume")]
[Authorize(Roles = "Admin")]
public class AdminResumeAnalysisController(AdminResumeService _resume) : ControllerBase
{
    /// <summary>Starts an analysis and streams its progress through to the admin page.</summary>
    [HttpPost("analyze")]
    public async Task Analyze([FromBody] AnalyzeRequest? request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var payload in _resume.StreamAnalysisAsync(request?.ResumeFileId, ct))
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
    public async Task<IEnumerable<ResumeAnalysisDto>> GetAnalyses([FromQuery] int take = 10, CancellationToken ct = default) =>
        await _resume.ListAnalysesAsync(take, ct);

    [HttpGet("analyses/{id:guid}")]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct) =>
        (await _resume.GetAnalysisAsync(id, ct)).ToActionResult(Ok);

    /// <summary>Applies a suggestion to the site data, optionally with edited values.</summary>
    [HttpPost("suggestions/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSuggestionRequest? request, CancellationToken ct) =>
        (await _resume.ApproveSuggestionAsync(id, request?.Overrides, ct))
            .ToActionResult(embeddingGenerated => Ok(new { Message = "Applied.", EmbeddingGenerated = embeddingGenerated }));

    [HttpPost("suggestions/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct) =>
        (await _resume.RejectSuggestionAsync(id, ct)).ToActionResult(NoContent);
}
