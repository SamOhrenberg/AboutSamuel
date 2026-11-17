using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.AdminMcp.OAuth;

/// <summary>RFC 7591 registration request. Only the fields this server uses.</summary>
public sealed class ClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")] public List<string>? RedirectUris { get; set; }
    [JsonPropertyName("client_name")] public string? ClientName { get; set; }
    [JsonPropertyName("token_endpoint_auth_method")] public string? TokenEndpointAuthMethod { get; set; }
    [JsonPropertyName("grant_types")] public List<string>? GrantTypes { get; set; }
    [JsonPropertyName("response_types")] public List<string>? ResponseTypes { get; set; }
}

public sealed record ClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes);

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("scope")] string Scope);

/// <summary>An OAuth error response body (RFC 6749 section 5.2).</summary>
public sealed record OAuthError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string Description)
{
    [JsonIgnore] public int StatusCode => Error == "invalid_client" ? 401 : 400;
}

/// <summary>The query of a GET /oauth/authorize.</summary>
public sealed record AuthorizeRequest(
    string? ResponseType, string? ClientId, string? RedirectUri, string? State, string? Scope,
    string? CodeChallenge, string? CodeChallengeMethod, string? Resource);

/// <summary>What /oauth/authorize does next.</summary>
public abstract record AuthorizeOutcome
{
    /// <summary>The request can't be trusted to redirect anywhere (unknown client or redirect URI).</summary>
    public sealed record ErrorPage(string Message) : AuthorizeOutcome;

    /// <summary>The redirect URI checked out, so the error goes back to the client.</summary>
    public sealed record RedirectError(string Url) : AuthorizeOutcome;

    /// <summary>The approval email is on its way; show the waiting page.</summary>
    public sealed record Started(Guid Id, string BrowserKey, string UserCode, string ClientName, DateTimeOffset ExpiresAt)
        : AuthorizeOutcome;
}

/// <summary>What the approval page shows about a pending sign-in.</summary>
public sealed record ApprovalView(string ClientName, string RedirectHost, string? RequestIp,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int AttemptsLeft);

public enum DecisionOutcome { Approved, Denied, WrongCode, LockedOut, Invalid }

/// <summary>The waiting page's poll result. Redirect is set once the sign-in is decided.</summary>
public sealed record PollResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("redirect")] string? Redirect = null);

public sealed record PollRequest(Guid Id, string? Key);
