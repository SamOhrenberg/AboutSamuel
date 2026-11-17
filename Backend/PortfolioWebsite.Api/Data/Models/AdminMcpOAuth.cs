namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// An OAuth client (Claude.ai, Claude Code...) that registered itself to use the admin MCP
/// server. Registration is open, as MCP expects, but redirect URIs come from an allowlist
/// and nothing gets a token without Samuel approving it by email.
/// </summary>
public class AdminMcpClient
{
    public string AdminMcpClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string RedirectUris { get; set; } = "[]";  // JSON array
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedIp { get; set; }
}

/// <summary>
/// One sign-in attempt, from the client sending the browser to /oauth/authorize until it
/// exchanges the code. Secrets are stored as SHA-256 hashes.
/// </summary>
public class AdminMcpAuthorization
{
    public Guid AdminMcpAuthorizationId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? State { get; set; }
    public string CodeChallenge { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;

    /// <summary>Held by the browser page that started the sign-in; only it can collect the code.</summary>
    public string BrowserKeyHash { get; set; } = string.Empty;
    /// <summary>The secret in the emailed approval link.</summary>
    public string ApprovalTokenHash { get; set; } = string.Empty;
    /// <summary>The short code shown on the sign-in page, typed on the approval page.</summary>
    public string UserCodeHash { get; set; } = string.Empty;
    public string? CodeHash { get; set; }

    /// <summary>pending, approved, denied, code_issued, completed.</summary>
    public string Status { get; set; } = "pending";
    public int FailedAttempts { get; set; }
    public string? RequestIp { get; set; }

    /// <summary>The refresh token family the code turned into, so a replayed code can revoke it.</summary>
    public Guid? FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

/// <summary>
/// A refresh token. Each use replaces it with a new one in the same family; using a replaced
/// token again revokes the whole family. A family can't outlive FamilyExpiresAt, after which
/// Samuel approves a new sign-in.
/// </summary>
public class AdminMcpRefreshToken
{
    public Guid AdminMcpRefreshTokenId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset FamilyExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
