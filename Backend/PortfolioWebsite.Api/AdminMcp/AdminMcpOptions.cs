namespace PortfolioWebsite.Api.AdminMcp;

/// <summary>Settings for the admin MCP server and its OAuth endpoints. Section "AdminMcp".</summary>
public sealed class AdminMcpOptions
{
    public const string SectionName = "AdminMcp";

    /// <summary>
    /// The API's public origin, as MCP clients reach it. It is the OAuth issuer, and the
    /// protected resource is this plus /mcp/admin. Behind Railway the request's own host
    /// can't be trusted for this, so it's configured.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "https://api.aboutsamuel.com";

    /// <summary>
    /// Redirect URIs a client may register, matched exactly. Claude.ai and the Claude apps
    /// use these two.
    /// </summary>
    public List<string> AllowedRedirectUris { get; set; } =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
    ];

    /// <summary>
    /// Also allow http://localhost and http://127.0.0.1 on any port and path, which is how
    /// desktop clients like Claude Code receive the code (RFC 8252).
    /// </summary>
    public bool AllowLoopbackRedirects { get; set; } = true;

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>How long one approval lasts, however often it's refreshed.</summary>
    public int SessionMaxDays { get; set; } = 90;

    /// <summary>How long the emailed approval link and the sign-in page stay valid.</summary>
    public int ApprovalMinutes { get; set; } = 10;

    /// <summary>Requests per minute to /mcp/admin. A guard against a runaway client.</summary>
    public int ToolRequestsPerMinute { get; set; } = 300;

    /// <summary>Sign-ins started per hour, across everyone. Each one emails Samuel.</summary>
    public int SignInsPerHour { get; set; } = 10;

    /// <summary>Per IP, per minute: registration, tokens, revocation and the approval page.</summary>
    public int OAuthRequestsPerMinute { get; set; } = 30;

    /// <summary>Per IP, per minute: the waiting page polls every two seconds.</summary>
    public int PollsPerMinute { get; set; } = 60;

    public string Issuer => PublicBaseUrl.TrimEnd('/');
    public string Resource => Issuer + AdminMcpRegistration.Endpoint;
}
