using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using PortfolioWebsite.Api.AdminMcp;
using PortfolioWebsite.Api.AdminMcp.OAuth;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>Captures approval emails instead of sending them.</summary>
public sealed class FakeApprovalMailer : IAdminMcpApprovalMailer
{
    private readonly ConcurrentQueue<AdminMcpApprovalEmail> _sent = new();
    public IReadOnlyList<AdminMcpApprovalEmail> Sent => [.. _sent];
    public bool Fail { get; set; }

    public Task SendAsync(AdminMcpApprovalEmail email, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("Email is down.");
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}

public sealed record OAuthTokens(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string RefreshToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
    [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
    [property: System.Text.Json.Serialization.JsonPropertyName("scope")] string Scope);

/// <summary>
/// Plays every part of the admin MCP sign-in over real HTTP: the client registering and
/// starting the flow, the browser page polling, and Samuel opening the email and approving.
/// </summary>
public sealed class AdminMcpSignIn(WebApplicationFactory<Program> factory)
{
    public const string RedirectUri = "https://claude.ai/api/mcp/auth_callback";

    public HttpClient Http { get; } = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public FakeApprovalMailer Mailer => (FakeApprovalMailer)factory.Services.GetRequiredService<IAdminMcpApprovalMailer>();

    /// <summary>Everything up to the code: register, authorize, approve, poll.</summary>
    public async Task<(string ClientId, string Code, string Verifier)> GetCodeAsync(string state = "xyz")
    {
        var clientId = await RegisterAsync();
        var verifier = NewVerifier();
        var page = await AuthorizeAsync(clientId, verifier, state);

        var (decision, _) = await ApproveAsync(page, page.UserCode);
        Assert.Equal(HttpStatusCode.OK, decision);

        var poll = await PollAsync(page);
        Assert.Equal("approved", poll.Status);
        var query = QueryHelpers.ParseQuery(new Uri(poll.Redirect!).Query);
        Assert.Equal(state, query["state"].ToString());
        return (clientId, query["code"].ToString(), verifier);
    }

    /// <summary>The whole flow, ending with tokens.</summary>
    public async Task<OAuthTokens> SignInAsync()
    {
        var (clientId, code, verifier) = await GetCodeAsync();
        using var response = await ExchangeAsync(clientId, code, verifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OAuthTokens>())!;
    }

    public async Task<string> RegisterAsync(string redirectUri = RedirectUri)
    {
        using var response = await Http.PostAsJsonAsync("/oauth/register", new
        {
            client_name = "Claude",
            redirect_uris = new[] { redirectUri },
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("client_id").GetString()!;
    }

    public sealed record WaitingPage(Guid Id, string Key, string UserCode, string ApprovalToken, string Html);

    public async Task<WaitingPage> AuthorizeAsync(string clientId, string verifier, string state = "xyz")
    {
        var before = Mailer.Sent.Count;
        using var response = await Http.GetAsync(AuthorizeUrl(clientId, S256(verifier), state));
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"authorize returned {response.StatusCode}: {html}");

        var cfg = Regex.Match(html, @"const cfg = (\{.*?\});").Groups[1].Value;
        using var doc = JsonDocument.Parse(cfg);
        var userCode = Regex.Match(html, @"aria-label=""Approval code"">([A-Z0-9-]+)<").Groups[1].Value;

        Assert.Equal(before + 1, Mailer.Sent.Count);
        var link = new Uri(Mailer.Sent[^1].ApprovalUrl);
        var token = QueryHelpers.ParseQuery(link.Query)["token"].ToString();

        return new(doc.RootElement.GetProperty("id").GetGuid(), doc.RootElement.GetProperty("key").GetString()!,
            userCode, token, html);
    }

    public static string AuthorizeUrl(string clientId, string challenge, string state = "xyz",
        string redirectUri = RedirectUri, string? resource = "http://localhost/mcp/admin") =>
        QueryHelpers.AddQueryString("/oauth/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["scope"] = "admin",
            ["resource"] = resource,
        });

    /// <summary>What Samuel does: open the emailed link (GET), then submit the form.</summary>
    public async Task<(HttpStatusCode Status, string Html)> ApproveAsync(WaitingPage page, string code, string decision = "approve")
    {
        using (var view = await Http.GetAsync($"/oauth/approve?token={Uri.EscapeDataString(page.ApprovalToken)}"))
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);

        using var response = await Http.PostAsync("/oauth/approve", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = page.ApprovalToken,
            ["code"] = code,
            ["decision"] = decision,
        }));
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public async Task<PollResult> PollAsync(WaitingPage page, string? key = null)
    {
        using var response = await Http.PostAsJsonAsync("/oauth/authorize/poll", new { id = page.Id, key = key ?? page.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PollResult>())!;
    }

    public Task<HttpResponseMessage> ExchangeAsync(string clientId, string code, string verifier,
        string redirectUri = RedirectUri) =>
        Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri,
            ["resource"] = "http://localhost/mcp/admin",
        }));

    public Task<HttpResponseMessage> RefreshAsync(string clientId, string refreshToken) =>
        Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
        }));

    /// <summary>A real McpClient on /mcp/admin with the given access token.</summary>
    public static Task<McpClient> ConnectAsync(WebApplicationFactory<Program> factory, string accessToken)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, AdminMcpRegistration.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http, NullLoggerFactory.Instance, ownsHttpClient: true);
        return McpClient.CreateAsync(transport);
    }

    public static string NewVerifier() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));

    public static string S256(string verifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
}
