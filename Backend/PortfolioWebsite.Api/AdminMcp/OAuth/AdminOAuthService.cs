using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using static PortfolioWebsite.Api.AdminMcp.AdminMcpTokens;

namespace PortfolioWebsite.Api.AdminMcp.OAuth;

/// <summary>
/// The OAuth 2.1 authorization server behind /mcp/admin, for one user: Samuel.
///
/// Clients register themselves (RFC 7591), then send the browser to /oauth/authorize with
/// PKCE. Instead of a password, that emails Samuel an approval link and shows a short code in
/// the browser. The approval page wants the code typed in, so a sign-in someone else started
/// can't be approved by mistake: Samuel never saw its code. Once approved, the page that
/// started the sign-in (and only it, by its browser key) collects the authorization code and
/// redirects back to the client.
///
/// Tokens: short-lived JWT access tokens, and rotating refresh tokens grouped into a session
/// (family) that is revoked as a whole when a used refresh token or code is replayed.
/// </summary>
public sealed class AdminOAuthService(
    SqlDbContext _db,
    IOptions<AdminMcpOptions> _options,
    AdminMcpTokens _tokens,
    IAdminMcpApprovalMailer _mailer,
    TimeProvider _time,
    ILogger<AdminOAuthService> _logger)
{
    public const int MaxRedirectUris = 5;
    public const int MaxClientNameLength = 200;
    public const int MaxCodeAttempts = 5;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

    private const string UserCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";  // no 0/O, 1/I/L
    private static readonly string[] SupportedGrants = ["authorization_code", "refresh_token"];

    private AdminMcpOptions Options => _options.Value;
    private DateTimeOffset Now => _time.GetUtcNow();

    // ---- registration ----------------------------------------------------------

    public async Task<(ClientRegistrationResponse? Client, OAuthError? Error)> RegisterAsync(
        ClientRegistrationRequest request, string? ip, CancellationToken ct = default)
    {
        var redirects = request.RedirectUris?.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList() ?? [];
        if (redirects.Count is 0 or > MaxRedirectUris)
            return (null, new("invalid_redirect_uri", $"Register between 1 and {MaxRedirectUris} redirect_uris."));
        var rejected = redirects.FirstOrDefault(u => !IsAllowedRedirect(u));
        if (rejected is not null)
            return (null, new("invalid_redirect_uri", "That redirect_uri isn't allowed by this server."));

        if (request.TokenEndpointAuthMethod is not (null or "none"))
            return (null, new("invalid_client_metadata", "Only public clients (token_endpoint_auth_method none) are supported."));
        if (request.GrantTypes is { } grants && !grants.All(SupportedGrants.Contains))
            return (null, new("invalid_client_metadata", "Supported grant_types are authorization_code and refresh_token."));
        if (request.ResponseTypes is { } responses && !responses.All(r => r == "code"))
            return (null, new("invalid_client_metadata", "The only supported response_type is code."));

        var name = request.ClientName?.Trim() is { Length: > 0 } n ? n : "MCP client";
        if (name.Length > MaxClientNameLength) name = name[..MaxClientNameLength];

        var client = new AdminMcpClient
        {
            AdminMcpClientId = NewSecret(),
            ClientName = name,
            RedirectUris = JsonSerializer.Serialize(redirects),
            CreatedAt = Now,
            CreatedIp = ip,
        };
        _db.AdminMcpClients.Add(client);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin MCP client registered: {ClientName} from {Ip}", name, ip);
        return (new ClientRegistrationResponse(client.AdminMcpClientId, client.CreatedAt.ToUnixTimeSeconds(), name,
            redirects, "none", SupportedGrants, ["code"]), null);
    }

    // ---- authorization ---------------------------------------------------------

    public async Task<AuthorizeOutcome> StartAsync(AuthorizeRequest q, string? ip, CancellationToken ct = default)
    {
        var client = string.IsNullOrEmpty(q.ClientId) ? null : await _db.AdminMcpClients.FindAsync([q.ClientId], ct);
        if (client is null)
            return new AuthorizeOutcome.ErrorPage("This app isn't registered with the server. Try connecting again.");

        var registered = JsonSerializer.Deserialize<List<string>>(client.RedirectUris) ?? [];
        var redirect = q.RedirectUri ?? (registered.Count == 1 ? registered[0] : null);
        if (redirect is null || !registered.Any(r => RedirectMatches(r, redirect)) || !IsAllowedRedirect(redirect))
            return new AuthorizeOutcome.ErrorPage("The app's redirect address doesn't match what it registered.");

        // From here errors go back to the client
        AuthorizeOutcome Fail(string error, string description) =>
            new AuthorizeOutcome.RedirectError(RedirectWith(redirect, q.State,
                ("error", error), ("error_description", description)));

        if (q.ResponseType != "code")
            return Fail("unsupported_response_type", "response_type must be code.");
        if (string.IsNullOrEmpty(q.CodeChallenge) || q.CodeChallengeMethod != "S256")
            return Fail("invalid_request", "PKCE with code_challenge_method S256 is required.");
        if (q.CodeChallenge.Length is < 43 or > 128)
            return Fail("invalid_request", "code_challenge has the wrong length.");
        if (!string.IsNullOrEmpty(q.Resource) && q.Resource.TrimEnd('/') != Options.Resource)
            return Fail("invalid_target", $"This server only issues tokens for {Options.Resource}.");
        var scope = string.IsNullOrWhiteSpace(q.Scope) ? AdminMcpTokens.Scope : q.Scope.Trim();
        if (scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s => s != AdminMcpTokens.Scope))
            return Fail("invalid_scope", $"The only scope is {AdminMcpTokens.Scope}.");

        await PurgeAsync(ct);

        var browserKey = NewSecret();
        var approvalToken = NewSecret();
        var userCode = NewUserCode();
        var row = new AdminMcpAuthorization
        {
            AdminMcpAuthorizationId = Guid.NewGuid(),
            ClientId = client.AdminMcpClientId,
            RedirectUri = redirect,
            State = q.State,
            CodeChallenge = q.CodeChallenge,
            Scope = AdminMcpTokens.Scope,
            BrowserKeyHash = Hash(browserKey),
            ApprovalTokenHash = Hash(approvalToken),
            UserCodeHash = Hash(NormalizeUserCode(userCode)),
            RequestIp = ip,
            CreatedAt = Now,
            ExpiresAt = Now.AddMinutes(Options.ApprovalMinutes),
        };
        _db.AdminMcpAuthorizations.Add(row);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _mailer.SendAsync(new AdminMcpApprovalEmail(
                $"{Options.Issuer}/oauth/approve?token={Uri.EscapeDataString(approvalToken)}",
                client.ClientName, HostOf(redirect), ip, row.ExpiresAt), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't send the admin MCP approval email");
            _db.AdminMcpAuthorizations.Remove(row);
            await _db.SaveChangesAsync(ct);
            return new AuthorizeOutcome.ErrorPage("The approval email couldn't be sent. Try again in a few minutes.");
        }

        _logger.LogInformation("Admin MCP sign-in started for {ClientName} from {Ip}", client.ClientName, ip);
        return new AuthorizeOutcome.Started(row.AdminMcpAuthorizationId, browserKey, userCode, client.ClientName, row.ExpiresAt);
    }

    /// <summary>The pending sign-in an approval link points at, or null if it's no longer pending.</summary>
    public async Task<ApprovalView?> GetApprovalAsync(string? approvalToken, CancellationToken ct = default)
    {
        var row = await FindPendingAsync(approvalToken, ct);
        if (row is null) return null;

        var client = await _db.AdminMcpClients.FindAsync([row.ClientId], ct);
        return new ApprovalView(client?.ClientName ?? "Unknown app", HostOf(row.RedirectUri), row.RequestIp,
            row.CreatedAt, row.ExpiresAt, MaxCodeAttempts - row.FailedAttempts);
    }

    public async Task<(DecisionOutcome Outcome, int AttemptsLeft)> DecideAsync(
        string? approvalToken, string? userCode, bool approve, CancellationToken ct = default)
    {
        var row = await FindPendingAsync(approvalToken, ct);
        if (row is null) return (DecisionOutcome.Invalid, 0);

        if (!approve)
        {
            row.Status = "denied";
            row.DecidedAt = Now;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Admin MCP sign-in {Id} denied", row.AdminMcpAuthorizationId);
            return (DecisionOutcome.Denied, 0);
        }

        if (!FixedTimeEquals(Hash(NormalizeUserCode(userCode)), row.UserCodeHash))
        {
            row.FailedAttempts++;
            var left = MaxCodeAttempts - row.FailedAttempts;
            if (left <= 0)
            {
                row.Status = "denied";
                row.DecidedAt = Now;
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Admin MCP sign-in {Id}: wrong code, {Left} attempts left", row.AdminMcpAuthorizationId, left);
            return left <= 0 ? (DecisionOutcome.LockedOut, 0) : (DecisionOutcome.WrongCode, left);
        }

        row.Status = "approved";
        row.DecidedAt = Now;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Admin MCP sign-in {Id} approved", row.AdminMcpAuthorizationId);
        return (DecisionOutcome.Approved, 0);
    }

    /// <summary>
    /// The waiting page asking whether Samuel has decided. On approval this mints the
    /// authorization code, once, and hands back the redirect that carries it.
    /// </summary>
    public async Task<PollResult> PollAsync(Guid id, string? browserKey, CancellationToken ct = default)
    {
        var row = await _db.AdminMcpAuthorizations.FindAsync([id], ct);
        if (row is null || string.IsNullOrEmpty(browserKey) || !FixedTimeEquals(Hash(browserKey), row.BrowserKeyHash))
            return new("invalid");

        if (row.Status is "pending" or "approved" && row.ExpiresAt <= Now)
            return new("expired");

        switch (row.Status)
        {
            case "pending":
                return new("pending");
            case "denied":
                return new("denied", RedirectWith(row.RedirectUri, row.State,
                    ("error", "access_denied"), ("error_description", "The sign-in was denied.")));
            case "approved":
                var code = NewSecret();
                row.CodeHash = Hash(code);
                row.Status = "code_issued";
                row.ExpiresAt = Now.Add(CodeLifetime);
                await _db.SaveChangesAsync(ct);
                return new("approved", RedirectWith(row.RedirectUri, row.State, ("code", code)));
            default:
                return new("invalid");  // the code was already handed out
        }
    }

    // ---- tokens ----------------------------------------------------------------

    public async Task<(TokenResponse? Tokens, OAuthError? Error)> ExchangeCodeAsync(
        string? clientId, string? code, string? redirectUri, string? codeVerifier, string? resource,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(codeVerifier))
            return (null, new("invalid_request", "client_id, code and code_verifier are required."));

        var row = await _db.AdminMcpAuthorizations.FirstOrDefaultAsync(a => a.CodeHash == Hash(code), ct);
        if (row is null)
            return (null, InvalidGrant("The code is invalid."));

        if (row.Status == "completed")
        {
            // A code used twice means someone else may have it: end whatever it started
            if (row.FamilyId is { } family) await RevokeFamilyAsync(family, ct);
            _logger.LogWarning("Admin MCP authorization code replayed; session revoked");
            return (null, InvalidGrant("The code was already used."));
        }
        if (row.Status != "code_issued" || row.ExpiresAt <= Now)
            return (null, InvalidGrant("The code is invalid or expired."));
        if (row.ClientId != clientId)
            return (null, InvalidGrant("The code was issued to another client."));
        if (redirectUri is not null && redirectUri != row.RedirectUri)
            return (null, InvalidGrant("redirect_uri doesn't match the authorization request."));
        if (codeVerifier.Length is < 43 or > 128 || !FixedTimeEquals(S256(codeVerifier), row.CodeChallenge))
            return (null, InvalidGrant("The code_verifier doesn't match the code_challenge."));
        if (!string.IsNullOrEmpty(resource) && resource.TrimEnd('/') != Options.Resource)
            return (null, new("invalid_target", $"This server only issues tokens for {Options.Resource}."));

        var familyId = Guid.NewGuid();
        row.Status = "completed";
        row.FamilyId = familyId;

        var tokens = await IssueAsync(clientId, familyId, Now.AddDays(Options.SessionMaxDays), row.Scope, ct);
        _logger.LogInformation("Admin MCP session {SessionId} started for client {ClientId}", familyId, clientId);
        return (tokens, null);
    }

    public async Task<(TokenResponse? Tokens, OAuthError? Error)> RefreshAsync(
        string? clientId, string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(refreshToken))
            return (null, new("invalid_request", "client_id and refresh_token are required."));

        var row = await _db.AdminMcpRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(refreshToken), ct);
        if (row is null || row.ClientId != clientId || row.RevokedAt is not null)
            return (null, InvalidGrant("The refresh token is invalid."));

        if (row.UsedAt is not null)
        {
            await RevokeFamilyAsync(row.FamilyId, ct);
            _logger.LogWarning("Admin MCP refresh token reused; session {SessionId} revoked", row.FamilyId);
            return (null, InvalidGrant("The refresh token was already used."));
        }
        if (row.ExpiresAt <= Now || row.FamilyExpiresAt <= Now)
            return (null, InvalidGrant("The session has expired. Connect again."));

        row.UsedAt = Now;
        return (await IssueAsync(clientId, row.FamilyId, row.FamilyExpiresAt, row.Scope, ct), null);
    }

    /// <summary>RFC 7009. Revoking a refresh token ends its whole session. Unknown tokens are fine.</summary>
    public async Task RevokeAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token)) return;
        var row = await _db.AdminMcpRefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == Hash(token), ct);
        if (row is not null)
        {
            await RevokeFamilyAsync(row.FamilyId, ct);
            _logger.LogInformation("Admin MCP session {SessionId} revoked by the client", row.FamilyId);
        }
    }

    /// <summary>Ends every admin MCP session. Returns how many were active.</summary>
    public async Task<int> RevokeAllAsync(CancellationToken ct = default)
    {
        var families = await _db.AdminMcpRefreshTokens
            .Where(t => t.RevokedAt == null && t.FamilyExpiresAt > Now)
            .Select(t => t.FamilyId).Distinct().CountAsync(ct);
        await _db.AdminMcpRefreshTokens.Where(t => t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);
        _logger.LogWarning("All admin MCP sessions revoked ({Count})", families);
        return families;
    }

    /// <summary>
    /// Whether an access token's session still stands. Checked on every request, so revoking
    /// takes effect immediately rather than when the access token expires.
    /// </summary>
    public Task<bool> IsSessionActiveAsync(Guid familyId, CancellationToken ct = default) =>
        _db.AdminMcpRefreshTokens.AnyAsync(
            t => t.FamilyId == familyId && t.RevokedAt == null && t.FamilyExpiresAt > Now, ct);

    // ---- helpers ---------------------------------------------------------------

    private async Task<TokenResponse> IssueAsync(string clientId, Guid familyId, DateTimeOffset familyExpiresAt,
        string scope, CancellationToken ct)
    {
        var refresh = NewSecret();
        var refreshExpires = Now.AddDays(Options.RefreshTokenDays);
        _db.AdminMcpRefreshTokens.Add(new AdminMcpRefreshToken
        {
            AdminMcpRefreshTokenId = Guid.NewGuid(),
            TokenHash = Hash(refresh),
            ClientId = clientId,
            FamilyId = familyId,
            Scope = scope,
            CreatedAt = Now,
            ExpiresAt = refreshExpires < familyExpiresAt ? refreshExpires : familyExpiresAt,
            FamilyExpiresAt = familyExpiresAt,
        });
        await _db.SaveChangesAsync(ct);

        var (access, expiresIn) = _tokens.CreateAccessToken(clientId, familyId, scope);
        return new TokenResponse(access, "Bearer", expiresIn, refresh, scope);
    }

    private Task RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        _db.AdminMcpRefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);

    private async Task<AdminMcpAuthorization?> FindPendingAsync(string? approvalToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(approvalToken)) return null;
        var row = await _db.AdminMcpAuthorizations.FirstOrDefaultAsync(a => a.ApprovalTokenHash == Hash(approvalToken), ct);
        return row is { Status: "pending" } && row.ExpiresAt > Now ? row : null;
    }

    /// <summary>Drops sign-ins over a day old and sessions that ended over a week ago.</summary>
    private async Task PurgeAsync(CancellationToken ct)
    {
        var dayAgo = Now.AddDays(-1);
        var weekAgo = Now.AddDays(-7);
        await _db.AdminMcpAuthorizations.Where(a => a.ExpiresAt < dayAgo).ExecuteDeleteAsync(ct);
        await _db.AdminMcpRefreshTokens
            .Where(t => t.FamilyExpiresAt < weekAgo || t.RevokedAt < weekAgo || t.ExpiresAt < weekAgo)
            .ExecuteDeleteAsync(ct);
    }

    private static OAuthError InvalidGrant(string description) => new("invalid_grant", description);

    private bool IsAllowedRedirect(string uri) =>
        Options.AllowedRedirectUris.Contains(uri, StringComparer.Ordinal)
        || (Options.AllowLoopbackRedirects && IsLoopback(uri));

    /// <summary>Exact match, except loopback redirects may use any port (RFC 8252 section 7.3).</summary>
    public static bool RedirectMatches(string registered, string requested)
    {
        if (string.Equals(registered, requested, StringComparison.Ordinal)) return true;
        if (!IsLoopback(registered) || !IsLoopback(requested)) return false;
        var a = new Uri(registered);
        var b = new Uri(requested);
        return a.Host == b.Host && a.AbsolutePath == b.AbsolutePath && a.Query == b.Query;
    }

    public static bool IsLoopback(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttp
        && string.IsNullOrEmpty(u.Fragment)
        && string.IsNullOrEmpty(u.UserInfo)
        && u.Host is "localhost" or "127.0.0.1" or "[::1]";

    private string RedirectWith(string redirectUri, string? state, params (string Key, string Value)[] values)
    {
        var query = values.ToDictionary(v => v.Key, v => (string?)v.Value);
        if (state is not null) query["state"] = state;
        query["iss"] = Options.Issuer;  // RFC 9207
        return QueryHelpers.AddQueryString(redirectUri, query);
    }

    private static string HostOf(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var u) ? u.Authority : uri;

    private static string NewUserCode()
    {
        var code = RandomNumberGenerator.GetString(UserCodeAlphabet, 8);
        return $"{code[..4]}-{code[4..]}";
    }

    /// <summary>Uppercase letters and digits only, so "abcd efgh" matches "ABCD-EFGH".</summary>
    public static string NormalizeUserCode(string? code) =>
        new((code ?? "").ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
}
