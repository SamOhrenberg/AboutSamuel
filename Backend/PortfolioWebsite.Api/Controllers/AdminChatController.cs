using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Dtos.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/chats")]
[Authorize(Roles = "Admin")]
public class AdminChatController(
    ILogger<AdminChatController> _logger,
    SqlDbContext _db) : ControllerBase
{
    /// <summary>
    /// Returns all chat messages grouped by SessionTrackingId.
    /// Sessions without a tracking ID are grouped together under null.
    /// Sessions ordered by most recent first.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<AdminChatSessionDto>> GetSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool errorsOnly = false)
    {
        _logger.LogInformation("Admin GET /admin/chats page={Page}", page);

        var query = _db.Chats.AsQueryable();

        if (errorsOnly)
            query = query.Where(c => c.Error);

        var chats = await query
            .OrderBy(c => c.ReceivedAt)
            .ToListAsync();

        // Group in memory by session
        var sessions = chats
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
                        TokenLimitReached = c.TokenLimitReached
                    })
                    .ToList()
            })
            .OrderByDescending(s => s.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return sessions;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var total = await _db.Chats.CountAsync();
        var errors = await _db.Chats.CountAsync(c => c.Error);
        var sessions = await _db.Chats.Select(c => c.SessionTrackingId).Distinct().CountAsync();
        var today = await _db.Chats.CountAsync(c => c.ReceivedAt >= DateTimeOffset.UtcNow.Date);
        var avgMs = await _db.Chats.AverageAsync(c => (double)c.ResponseTookMs);

        return Ok(new
        {
            TotalMessages = total,
            TotalSessions = sessions,
            ErrorCount = errors,
            MessagesToday = today,
            AvgResponseMs = Math.Round(avgMs, 1)
        });
    }
}