using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>
/// Adversarial test runs against SamuelLM. The agent service does the work and writes
/// the results; this controller starts runs and reads them back for the admin panel.
/// </summary>
[ApiController]
[Route("admin/adversarial")]
[Authorize(Roles = "Admin")]
public class AdminAdversarialController(AdminActivityService _activity) : ControllerBase
{
    [HttpPost("runs")]
    public async Task<IActionResult> StartRun(CancellationToken ct) =>
        (await _activity.StartAdversarialRunAsync(ct)).ToActionResult(runId => Accepted(new { runId }));

    [HttpGet("runs")]
    public async Task<IEnumerable<AdminAdversarialRunDto>> GetRuns([FromQuery] int take = 20, CancellationToken ct = default) =>
        await _activity.ListAdversarialRunsAsync(take, ct);

    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken ct) =>
        (await _activity.GetAdversarialRunAsync(id, ct)).ToActionResult(Ok);
}
