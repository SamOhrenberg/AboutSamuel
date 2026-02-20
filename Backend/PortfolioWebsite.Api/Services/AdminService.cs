using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PortfolioWebsite.Api.Services;

public class AdminService
{
    private readonly SqlDbContext _db;
    private readonly MailgunService _mailgun;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminService> _logger;

    // How long a magic link is valid
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromMinutes(15);
    // How long the resulting JWT is valid
    private static readonly TimeSpan JwtExpiry = TimeSpan.FromHours(1);

    public AdminService(
        SqlDbContext db,
        MailgunService mailgun,
        IConfiguration configuration,
        ILogger<AdminService> logger)
    {
        _db = db;
        _mailgun = mailgun;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendMagicLinkAsync(string requestIp)
    {
        // Purge expired/used tokens before creating a new one
        await PurgeExpiredTokensAsync();

        // Generate a cryptographically secure random token
        var rawToken = GenerateSecureToken();
        var tokenHash = HashToken(rawToken);

        var adminToken = new AdminToken
        {
            AdminTokenId = Guid.NewGuid(),
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenExpiry),
            RequestIp = requestIp
        };

        await _db.AdminTokens.AddAsync(adminToken);
        await _db.SaveChangesAsync();

        var baseUrl = _configuration.GetValue<string>("AdminSettings:BaseUrl")
            ?? "https://aboutsamuel.com";
        var magicLink = $"{baseUrl}/admin/verify?token={Uri.EscapeDataString(rawToken)}";

        var adminEmail = _configuration.GetValue<string>("AdminSettings:AdminEmail")
            ?? throw new InvalidOperationException("AdminSettings:AdminEmail must be configured.");

        var body = $"""
            <p>Hello Samuel,</p>
            <p>A login was requested for the AboutSamuel admin panel.</p>
            <p><a href="{magicLink}" style="background:#007c7c;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;display:inline-block;">
                Log In to Admin Panel
            </a></p>
            <p>This link expires in <strong>15 minutes</strong> and can only be used once.</p>
            <p>If you did not request this, you can safely ignore this email.</p>
            <p style="color:#999;font-size:12px;">Requested from IP: {requestIp}</p>
            """;

        await _mailgun.SendEmailAsync(adminEmail, "Admin Login — AboutSamuel.com", body);

        _logger.LogInformation("Magic link sent to admin from IP {Ip}", requestIp);
    }

    public async Task<(bool Success, string? Jwt, DateTimeOffset ExpiresAt)> VerifyTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);

        var record = await _db.AdminTokens
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                !t.Used &&
                t.ExpiresAt > DateTimeOffset.UtcNow);

        if (record is null)
        {
            _logger.LogWarning("Admin token verification failed — token not found or expired");
            return (false, null, default);
        }

        // Mark as used immediately (single-use)
        record.Used = true;
        await _db.SaveChangesAsync();

        var expiresAt = DateTimeOffset.UtcNow.Add(JwtExpiry);
        var jwt = GenerateJwt(expiresAt);

        _logger.LogInformation("Admin token verified successfully, JWT issued");
        return (true, jwt, expiresAt);
    }

    private string GenerateJwt(DateTimeOffset expiresAt)
    {
        var secret = _configuration.GetValue<string>("AdminSettings:JwtSecret")
            ?? throw new InvalidOperationException("AdminSettings:JwtSecret must be configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iss, "aboutsamuel.com"),
            new Claim(JwtRegisteredClaimNames.Aud, "aboutsamuel-admin"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLower();
    }

    private async Task PurgeExpiredTokensAsync()
    {
        var cutoff = DateTimeOffset.UtcNow;
        var expired = await _db.AdminTokens
            .Where(t => t.Used || t.ExpiresAt <= cutoff)
            .ToListAsync();

        if (expired.Count > 0)
        {
            _db.AdminTokens.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }
}