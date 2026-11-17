using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/chats")]
[Authorize(Roles = "Admin")]
public class AdminChatController(AdminActivityService _activity) : ControllerBase
{
    /// <summary>
    /// Returns all chat messages grouped by SessionTrackingId. Sessions without a tracking ID
    /// are grouped together under null. Sessions ordered by most recent first.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<AdminChatSessionDto>> GetSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool errorsOnly = false,
        CancellationToken ct = default) =>
        await _activity.ListChatSessionsAsync(page, pageSize, errorsOnly, ct);

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct) => Ok(await _activity.GetChatStatsAsync(ct));
}
