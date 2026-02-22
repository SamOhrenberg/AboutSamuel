using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController(
    ILogger<AdminController> _logger,
    AdminService _adminService,
    SqlDbContext _dbContext,
    EmbeddingService _embeddingService) : ControllerBase
{
    /// <summary>
    /// Sends a magic link to the configured admin email.
    /// Rate limited to 5 requests per hour per IP.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("AdminLogin")]
    public async Task<IActionResult> Login()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogInformation("Admin login requested from {Ip}", ip);
        try
        {
            await _adminService.SendMagicLinkAsync(ip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending admin magic link");
        }
        return Ok(new { Message = "If the email is recognized, a login link has been sent." });
    }

    /// <summary>
    /// Exchanges a magic link token for a short-lived JWT.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] AdminVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { Message = "Token is required." });

        var (success, jwt, expiresAt) = await _adminService.VerifyTokenAsync(request.Token);
        if (!success)
            return Unauthorized(new { Message = "This link is invalid or has expired. Please request a new one." });

        return Ok(new AdminAuthResponse { Jwt = jwt!, ExpiresAt = expiresAt });
    }

    /// <summary>
    /// Generates and stores embeddings for all Information, Project, and WorkExperience
    /// entries that don't have one yet. Safe to call multiple times — skips existing.
    /// Requires admin JWT.
    /// </summary>
    [HttpPost("generate-embeddings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddings()
    {
        var infoEntries = await _dbContext.Information
            .Where(i => i.EmbeddingJson == null && i.Text != null)
            .ToListAsync();

        var projects = await _dbContext.Projects
            .Where(p => p.EmbeddingJson == null && p.IsActive)
            .ToListAsync();

        var workEntries = await _dbContext.WorkExperiences
            .Where(w => w.EmbeddingJson == null && w.IsActive)
            .ToListAsync();

        int success = 0, failed = 0;

        foreach (var entry in infoEntries)
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(entry.Text!);
            if (embedding != null)
            { entry.EmbeddingJson = EmbeddingService.SerializeEmbedding(embedding); success++; }
            else failed++;
            await Task.Delay(100);
        }

        foreach (var project in projects)
        {
            var text = ChatService.BuildProjectRagText(project);
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding != null)
            { project.EmbeddingJson = EmbeddingService.SerializeEmbedding(embedding); success++; }
            else failed++;
            await Task.Delay(100);
        }

        foreach (var job in workEntries)
        {
            var text = ChatService.BuildWorkRagText(job);
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding != null)
            { job.EmbeddingJson = EmbeddingService.SerializeEmbedding(embedding); success++; }
            else failed++;
            await Task.Delay(100);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Embedding generation complete: {Success} succeeded, {Failed} failed",
            success, failed);

        return Ok(new { success, failed, total = infoEntries.Count + projects.Count + workEntries.Count });
    }

    /// <summary>
    /// Regenerates the embedding for a single item.
    /// type must be "information", "project", or "work-experience"
    /// </summary>
    [HttpPost("generate-embeddings/{type}/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddingForItem(string type, Guid id)
    {
        string? text = null;
        Action<string> persist = _ => { };

        switch (type.ToLower())
        {
            case "information":
                {
                    var entry = await _dbContext.Information.FindAsync(id);
                    if (entry is null) return NotFound();
                    text = entry.Text;
                    persist = json => entry.EmbeddingJson = json;
                    break;
                }
            case "project":
                {
                    var project = await _dbContext.Projects
                        .Include(p => p.WorkExperience)
                        .FirstOrDefaultAsync(p => p.ProjectId == id);
                    if (project is null) return NotFound();
                    text = ChatService.BuildProjectRagText(project);
                    persist = json => project.EmbeddingJson = json;
                    break;
                }
            case "work-experience":
                {
                    var job = await _dbContext.WorkExperiences.FindAsync(id);
                    if (job is null) return NotFound();
                    text = ChatService.BuildWorkRagText(job);
                    persist = json => job.EmbeddingJson = json;
                    break;
                }
            default:
                return BadRequest(new { Message = "type must be 'information', 'project', or 'work-experience'" });
        }

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { Message = "Entry has no text to embed." });

        var embedding = await _embeddingService.GetEmbeddingAsync(text);
        if (embedding is null)
            return StatusCode(502, new { Message = "Embedding service failed. Check Bedrock logs." });

        persist(EmbeddingService.SerializeEmbedding(embedding));
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Embedding generated successfully." });
    }
}