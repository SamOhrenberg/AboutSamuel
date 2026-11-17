using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>Recruiter triage review for the admin panel. The logic is in AdminRecruiterService.</summary>
[ApiController]
[Route("admin/recruiters")]
[Authorize(Roles = "Admin")]
public class AdminRecruiterController(AdminRecruiterService _recruiters) : ControllerBase
{
    [HttpGet("stats")]
    public Task<AdminRecruiterStatsDto> GetStats(CancellationToken ct) => _recruiters.GetStatsAsync(ct);

    [HttpGet("postings")]
    public async Task<IEnumerable<AdminRecruiterPostingDto>> GetPostings(
        [FromQuery] string? reviewStatus = null, [FromQuery] string? outcome = null, [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        await _recruiters.ListPostingsAsync(reviewStatus, outcome, take, ct);

    [HttpGet("postings/{id:guid}")]
    public async Task<IActionResult> GetPosting(Guid id, CancellationToken ct) =>
        (await _recruiters.GetPostingAsync(id, ct)).ToActionResult(Ok);

    [HttpPatch("postings/{id:guid}")]
    public async Task<IActionResult> ReviewPosting(Guid id, [FromBody] AdminRecruiterReviewRequest request,
        CancellationToken ct) =>
        (await _recruiters.ReviewPostingAsync(id, request, ct)).ToActionResult(NoContent);

    public record MergeRequest(List<Guid> PostingIds);

    [HttpPost("postings/merge")]
    public async Task<IActionResult> MergePostings([FromBody] MergeRequest request, CancellationToken ct) =>
        (await _recruiters.MergePostingsAsync(request.PostingIds, ct))
            .ToActionResult(target => Ok(new { recruiterPostingId = target }));

    [HttpGet("settings")]
    public Task<AdminRecruiterSettingsDto> GetSettings(CancellationToken ct) => _recruiters.GetSettingsAsync(ct);

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] AdminRecruiterSettingsDto dto, CancellationToken ct) =>
        (await _recruiters.SaveSettingsAsync(dto, ct)).ToActionResult(Ok);
}
