using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pgvector;
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
    IEmbeddingService _embeddingService) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("AdminLogin")]
    public async Task<IActionResult> Login()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogInformation("Admin login requested from {Ip}", ip);
        try { await _adminService.SendMagicLinkAsync(ip); }
        catch (Exception ex) { _logger.LogError(ex, "Error sending admin magic link"); }
        return Ok(new { Message = "If the email is recognized, a login link has been sent." });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] AdminVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { Message = "Token is required." });

        var (success, jwt, expiresAt) = await _adminService.VerifyTokenAsync(request.Token);
        if (!success)
            return Unauthorized(new { Message = "This link is invalid or has expired." });

        return Ok(new AdminAuthResponse { Jwt = jwt!, ExpiresAt = expiresAt });
    }

    /// <summary>
    /// Generates and stores embeddings for all entries that don't have one yet.
    /// Now stores into the native vector(1536) column.
    /// </summary>
    [HttpPost("generate-embeddings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddings()
    {
        var infoEntries = await _dbContext.Information
            .Where(i => i.Embedding == null && i.Text != null)
            .ToListAsync();

        var projects = await _dbContext.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.Embedding == null && p.IsActive)
            .ToListAsync();

        var workEntries = await _dbContext.WorkExperiences
            .Where(w => w.Embedding == null && w.IsActive)
            .ToListAsync();

        int success = 0, failed = 0;

        foreach (var entry in infoEntries)
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(entry.Text!);
            if (embedding != null)
            {
                entry.Embedding = new Vector(embedding);
                success++;
            }
            else failed++;
            await Task.Delay(50);
        }

        foreach (var project in projects)
        {
            var text = ChatService.BuildProjectRagText(project);
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding != null)
            {
                project.Embedding = new Vector(embedding);
                success++;
            }
            else failed++;
            await Task.Delay(50);
        }

        foreach (var job in workEntries)
        {
            var text = ChatService.BuildWorkRagText(job);
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding != null)
            {
                job.Embedding = new Vector(embedding);
                success++;
            }
            else failed++;
            await Task.Delay(50);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Embedding generation complete: {Success} succeeded, {Failed} failed",
            success, failed);

        return Ok(new
        {
            success,
            failed,
            total = infoEntries.Count + projects.Count + workEntries.Count
        });
    }

    [HttpPost("generate-embeddings/{type}/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddingForItem(string type, Guid id)
    {
        string? text = null;
        Action<float[]> persist = _ => { };

        switch (type.ToLower())
        {
            case "information":
                {
                    var entry = await _dbContext.Information.FindAsync(id);
                    if (entry is null) return NotFound();
                    text = entry.Text;
                    persist = v =>
                    {
                        entry.Embedding = new Vector(v);
                    };
                    break;
                }
            case "project":
                {
                    var project = await _dbContext.Projects
                        .Include(p => p.WorkExperiences)
                        .FirstOrDefaultAsync(p => p.ProjectId == id);
                    if (project is null) return NotFound();
                    text = ChatService.BuildProjectRagText(project);
                    persist = v =>
                    {
                        project.Embedding = new Vector(v);
                    };
                    break;
                }
            case "work-experience":
                {
                    var job = await _dbContext.WorkExperiences.FindAsync(id);
                    if (job is null) return NotFound();
                    text = ChatService.BuildWorkRagText(job);
                    persist = v =>
                    {
                        job.Embedding = new Vector(v);
                    };
                    break;
                }
            default:
                return BadRequest(new { Message = "type must be 'information', 'project', or 'work-experience'" });
        }

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { Message = "Entry has no text to embed." });

        var embedding = await _embeddingService.GetEmbeddingAsync(text);
        if (embedding is null)
            return StatusCode(502, new { Message = "Embedding service failed." });

        persist(embedding);
        await _dbContext.SaveChangesAsync();

        return Ok(new { Message = "Embedding generated successfully." });
    }
}
