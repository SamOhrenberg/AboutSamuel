using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>A token like the admin panel gets after the magic link, signed with ApiFactory's secret.</summary>
public static class AdminPanelToken
{
    public const string Secret = "integration-tests-only-secret-0123456789abcdef";

    public static string Create()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(JwtRegisteredClaimNames.Iss, "aboutsamuel.com"),
                new Claim(JwtRegisteredClaimNames.Aud, "aboutsamuel-admin"),
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
