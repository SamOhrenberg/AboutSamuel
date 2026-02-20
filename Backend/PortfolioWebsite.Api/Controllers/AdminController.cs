using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController(
    ILogger<AdminController> _logger,
    AdminService _adminService) : ControllerBase
{
    /// <summary>
    /// Sends a magic link to the configured admin email.
    /// Rate limited to 5 requests per hour per IP.
    /// The email address is not accepted as input — it is always sent
    /// to the configured AdminSettings:AdminEmail to prevent enumeration.
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
            // Don't leak whether the email send succeeded — always return 200
            // to avoid confirming that the endpoint exists and is functional.
            _logger.LogError(ex, "Error sending admin magic link");
        }

        // Always return 200 regardless of outcome (prevents oracle attacks)
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
        {
            // Return 401 — don't say whether the token was expired vs invalid
            return Unauthorized(new { Message = "This link is invalid or has expired. Please request a new one." });
        }

        return Ok(new AdminAuthResponse { Jwt = jwt!, ExpiresAt = expiresAt });
    }
}