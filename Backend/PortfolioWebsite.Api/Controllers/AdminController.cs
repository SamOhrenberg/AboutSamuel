using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController(
    ILogger<AdminController> _logger,
    AdminService _adminService,
    AdminEmbeddingService _embeddings) : ControllerBase
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

    /// <summary>Generates and stores embeddings for all entries that don't have one yet.</summary>
    [HttpPost("generate-embeddings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddings(CancellationToken ct)
    {
        var result = await _embeddings.GenerateMissingAsync(ct);
        return Ok(new { success = result.Success, failed = result.Failed, total = result.Total });
    }

    [HttpPost("generate-embeddings/{type}/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateEmbeddingForItem(string type, Guid id, CancellationToken ct) =>
        (await _embeddings.GenerateForItemAsync(type, id, ct))
            .ToActionResult(() => Ok(new { Message = "Embedding generated successfully." }));

    [HttpPost("embedding-visualization/regenerate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegenerateVisualization() =>
        (await _embeddings.RegenerateSkillMapAsync())
            .ToActionResult(count => Ok(new { Message = $"Visualization regenerated with {count} points." }));
}
