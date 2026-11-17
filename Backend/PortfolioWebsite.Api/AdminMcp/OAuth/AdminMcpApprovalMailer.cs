using System.Net;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.AdminMcp.OAuth;

public sealed record AdminMcpApprovalEmail(
    string ApprovalUrl, string ClientName, string RedirectHost, string? RequestIp, DateTimeOffset ExpiresAt);

/// <summary>Sends Samuel the link that approves an MCP sign-in. Tests swap it for a fake.</summary>
public interface IAdminMcpApprovalMailer
{
    Task SendAsync(AdminMcpApprovalEmail email, CancellationToken ct = default);
}

public sealed class AdminMcpApprovalMailer(AzureEmailService email, IConfiguration configuration) : IAdminMcpApprovalMailer
{
    public async Task SendAsync(AdminMcpApprovalEmail approval, CancellationToken ct = default)
    {
        var to = configuration.GetValue<string>("AdminSettings:AdminEmail")
            ?? throw new InvalidOperationException("AdminSettings:AdminEmail must be configured.");

        // The client name is whatever the client registered with, so it's encoded like any input
        var client = WebUtility.HtmlEncode(approval.ClientName);
        var host = WebUtility.HtmlEncode(approval.RedirectHost);
        var ip = WebUtility.HtmlEncode(approval.RequestIp ?? "unknown");

        var body = $"""
            <p>Hello Samuel,</p>
            <p><strong>{client}</strong> is asking for access to the AboutSamuel admin MCP server,
            which can read and change everything in the admin panel. It will send the result to
            <strong>{host}</strong>.</p>
            <p>Only approve this if you just clicked Connect yourself. The approval page asks for
            the code shown in the window where you started.</p>
            <p><a href="{approval.ApprovalUrl}" style="background:#007c7c;color:white;padding:12px 24px;text-decoration:none;border-radius:6px;display:inline-block;">
                Review sign-in
            </a></p>
            <p>This link expires at {approval.ExpiresAt:HH:mm} UTC and works once.</p>
            <p style="color:#999;font-size:12px;">Requested from IP: {ip}</p>
            """;

        await email.SendEmailAsync(to, "Admin MCP sign-in request - AboutSamuel.com", body);
    }
}
