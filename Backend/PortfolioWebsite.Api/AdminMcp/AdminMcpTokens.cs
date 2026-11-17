using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PortfolioWebsite.Api.AdminMcp;

/// <summary>
/// Access tokens for /mcp/admin. They're JWTs signed with a key derived from
/// AdminSettings:JwtSecret, so there's no new secret to configure and rotating that secret
/// ends every session, but a token for the admin panel and a token for MCP can never stand in
/// for each other: different key, different audience.
/// </summary>
public sealed class AdminMcpTokens(IOptions<AdminMcpOptions> options, IConfiguration configuration, TimeProvider time)
{
    public const string Scope = "admin";
    public const string ClientIdClaim = "client_id";
    public const string SessionClaim = "sid";
    public const string ScopeClaim = "scope";
    public const string Subject = "samuel";

    private readonly SymmetricSecurityKey _key = SigningKey(configuration);

    /// <summary>The MCP signing key: HMAC of a fixed label under the admin JWT secret.</summary>
    public static SymmetricSecurityKey SigningKey(IConfiguration configuration)
    {
        var secret = configuration.GetValue<string>("AdminSettings:JwtSecret")
            ?? throw new InvalidOperationException("AdminSettings:JwtSecret must be configured.");
        var key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), "aboutsamuel-admin-mcp/v1"u8);
        return new SymmetricSecurityKey(key);
    }

    public static TokenValidationParameters ValidationParameters(AdminMcpOptions options, SymmetricSecurityKey key) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Resource,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // Claims stay as written (the bearer handler is set not to map them), so these name
        // the types CreateAccessToken uses. AuthenticationType is what IsAdminMcpCaller checks.
        AuthenticationType = AdminMcpRegistration.AuthScheme,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = JwtRegisteredClaimNames.Sub,
    };

    public (string Token, int ExpiresInSeconds) CreateAccessToken(string clientId, Guid sessionId, string scope)
    {
        var o = options.Value;
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(o.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Resource,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Subject),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClientIdClaim, clientId),
                new Claim(SessionClaim, sessionId.ToString()),
                new Claim(ScopeClaim, scope),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), o.AccessTokenMinutes * 60);
    }

    /// <summary>True for a caller holding a valid admin MCP access token.</summary>
    public static bool IsAdminMcpCaller(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
        && user.Identity.AuthenticationType == AdminMcpRegistration.AuthScheme
        && user.IsInRole("Admin")
        && user.FindFirst(ScopeClaim)?.Value.Split(' ').Contains(Scope) == true;

    // ---- opaque secrets ------------------------------------------------------

    /// <summary>A URL-safe random secret with 256 bits of entropy.</summary>
    public static string NewSecret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>PKCE S256: BASE64URL(SHA256(verifier)).</summary>
    public static string S256(string verifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
