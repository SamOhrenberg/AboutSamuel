using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using PortfolioWebsite.Api.AdminMcp;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration.AdminMcp;

/// <summary>
/// The admin MCP authorization server end to end: discovery, registration, the emailed
/// approval with its typed code, PKCE, refresh rotation and revocation.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminMcpOAuthTests(ApiFactory factory)
{
    private readonly AdminMcpSignIn _signIn = new(factory);
    private HttpClient Http => _signIn.Http;

    // ---- discovery ---------------------------------------------------------------------

    [Fact]
    public async Task Unauthenticated_requests_get_401_pointing_at_the_resource_metadata()
    {
        using var response = await Http.PostAsync(AdminMcpRegistration.Endpoint, JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains("resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/mcp/admin\"",
            challenge.Parameter);
    }

    [Fact]
    public async Task Resource_metadata_names_the_admin_endpoint_and_this_issuer()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(AdminMcpRegistration.ProtectedResourceMetadataPath));

        Assert.Equal("http://localhost/mcp/admin", doc.RootElement.GetProperty("resource").GetString());
        Assert.Equal(["http://localhost"], doc.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [InlineData("/.well-known/oauth-protected-resource")]
    [InlineData("/.well-known/oauth-protected-resource/mcp")]
    public async Task The_public_server_advertises_no_authorization(string path)
    {
        using var response = await Http.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_server_metadata_lists_the_endpoints()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync("/.well-known/oauth-authorization-server"));
        var root = doc.RootElement;

        Assert.Equal("http://localhost", root.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost/oauth/authorize", root.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/oauth/token", root.GetProperty("token_endpoint").GetString());
        Assert.Equal("http://localhost/oauth/register", root.GetProperty("registration_endpoint").GetString());
        Assert.Equal(["S256"], root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(e => e.GetString()));
    }

    // ---- registration --------------------------------------------------------------------

    [Theory]
    [InlineData("https://evil.example/callback")]
    [InlineData("https://claude.ai/api/mcp/auth_callback/../other")]
    [InlineData("http://localhost.evil.example/callback")]
    public async Task Registration_refuses_redirects_off_the_allowlist(string redirect)
    {
        using var response = await Http.PostAsJsonAsync("/oauth/register", new { redirect_uris = new[] { redirect } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_redirect_uri", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Loopback_redirects_register_and_match_on_any_port()
    {
        var clientId = await _signIn.RegisterAsync("http://127.0.0.1:33418/callback");
        var challenge = AdminMcpSignIn.S256(AdminMcpSignIn.NewVerifier());

        using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl(clientId, challenge,
            redirectUri: "http://127.0.0.1:50001/callback"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- authorize ---------------------------------------------------------------------

    [Fact]
    public async Task Unknown_client_gets_an_error_page_not_a_redirect()
    {
        using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl("nope", "x".PadRight(43, 'x')));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Unregistered_redirect_gets_an_error_page_not_a_redirect()
    {
        var clientId = await _signIn.RegisterAsync();

        using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl(clientId, "x".PadRight(43, 'x'),
            redirectUri: "https://claude.com/api/mcp/auth_callback"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Missing_pkce_redirects_back_with_an_error()
    {
        var clientId = await _signIn.RegisterAsync();
        var url = AdminMcpSignIn.AuthorizeUrl(clientId, "x".PadRight(43, 'x')).Replace("code_challenge_method=S256", "code_challenge_method=plain");

        using var response = await Http.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("invalid_request", query["error"].ToString());
        Assert.Equal("xyz", query["state"].ToString());
    }

    [Fact]
    public async Task A_token_for_another_resource_is_refused()
    {
        var clientId = await _signIn.RegisterAsync();

        using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl(clientId, "x".PadRight(43, 'x'),
            resource: "http://localhost/mcp"));

        Assert.Equal("invalid_target", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].ToString());
    }

    [Fact]
    public async Task Failed_email_shows_an_error_and_starts_nothing()
    {
        var clientId = await _signIn.RegisterAsync();
        _signIn.Mailer.Fail = true;
        try
        {
            using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl(clientId, "x".PadRight(43, 'x')));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            _signIn.Mailer.Fail = false;
        }
    }

    [Fact]
    public async Task Waiting_page_has_a_strict_csp_and_encodes_the_client_name()
    {
        using var register = await Http.PostAsJsonAsync("/oauth/register", new
        {
            client_name = "<script>alert(1)</script>",
            redirect_uris = new[] { AdminMcpSignIn.RedirectUri },
        });
        using var doc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var clientId = doc.RootElement.GetProperty("client_id").GetString()!;

        using var response = await Http.GetAsync(AdminMcpSignIn.AuthorizeUrl(clientId, "x".PadRight(43, 'x')));
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'nonce-", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    // ---- approval ------------------------------------------------------------------------

    [Fact]
    public async Task Poll_stays_pending_until_approved_and_only_answers_the_starting_page()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());

        Assert.Equal("pending", (await _signIn.PollAsync(page)).Status);
        Assert.Equal("invalid", (await _signIn.PollAsync(page, key: "someone-else")).Status);
    }

    [Fact]
    public async Task Opening_the_link_does_not_approve()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());

        using var view = await Http.GetAsync($"/oauth/approve?token={Uri.EscapeDataString(page.ApprovalToken)}");

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal("pending", (await _signIn.PollAsync(page)).Status);
    }

    [Fact]
    public async Task Wrong_code_does_not_approve_and_five_wrong_codes_deny()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());

        for (var i = 0; i < 4; i++)
        {
            var (_, html) = await _signIn.ApproveAsync(page, "AAAA-AAAA");
            Assert.Contains("doesn&#39;t match", html);
            Assert.Equal("pending", (await _signIn.PollAsync(page)).Status);
        }

        var (status, _) = await _signIn.ApproveAsync(page, "AAAA-AAAA");
        Assert.Equal(HttpStatusCode.BadRequest, status);

        var poll = await _signIn.PollAsync(page);
        Assert.Equal("denied", poll.Status);
        Assert.Equal("access_denied", QueryHelpers.ParseQuery(new Uri(poll.Redirect!).Query)["error"].ToString());
    }

    [Fact]
    public async Task The_code_is_accepted_without_dash_or_case()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());

        var (status, _) = await _signIn.ApproveAsync(page, page.UserCode.Replace("-", " ").ToLowerInvariant());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("approved", (await _signIn.PollAsync(page)).Status);
    }

    [Fact]
    public async Task Deny_sends_access_denied_back()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());

        await _signIn.ApproveAsync(page, "", decision: "deny");

        Assert.Equal("denied", (await _signIn.PollAsync(page)).Status);
    }

    [Fact]
    public async Task An_approval_link_works_once()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());
        await _signIn.ApproveAsync(page, page.UserCode);

        using var again = await Http.GetAsync($"/oauth/approve?token={Uri.EscapeDataString(page.ApprovalToken)}");

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task The_code_is_handed_out_once()
    {
        var clientId = await _signIn.RegisterAsync();
        var page = await _signIn.AuthorizeAsync(clientId, AdminMcpSignIn.NewVerifier());
        await _signIn.ApproveAsync(page, page.UserCode);

        Assert.Equal("approved", (await _signIn.PollAsync(page)).Status);
        Assert.Equal("invalid", (await _signIn.PollAsync(page)).Status);
    }

    // ---- tokens ----------------------------------------------------------------------------

    [Fact]
    public async Task The_full_flow_yields_tokens()
    {
        var tokens = await _signIn.SignInAsync();

        Assert.Equal("Bearer", tokens.TokenType);
        Assert.Equal("admin", tokens.Scope);
        Assert.Equal(3600, tokens.ExpiresIn);
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task A_wrong_verifier_is_refused()
    {
        var (clientId, code, _) = await _signIn.GetCodeAsync();

        using var response = await _signIn.ExchangeAsync(clientId, code, AdminMcpSignIn.NewVerifier());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_grant", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_code_for_another_client_is_refused()
    {
        var (_, code, verifier) = await _signIn.GetCodeAsync();
        var otherClient = await _signIn.RegisterAsync();

        using var response = await _signIn.ExchangeAsync(otherClient, code, verifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replaying_a_code_revokes_the_session_it_started()
    {
        var (clientId, code, verifier) = await _signIn.GetCodeAsync();
        using var first = await _signIn.ExchangeAsync(clientId, code, verifier);
        var tokens = (await first.Content.ReadFromJsonAsync<OAuthTokens>())!;

        using var replay = await _signIn.ExchangeAsync(clientId, code, verifier);

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await AdminPingStatusAsync(tokens.AccessToken));
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_the_session()
    {
        var (clientId, code, verifier) = await _signIn.GetCodeAsync();
        using var exchange = await _signIn.ExchangeAsync(clientId, code, verifier);
        var first = (await exchange.Content.ReadFromJsonAsync<OAuthTokens>())!;

        using var refresh = await _signIn.RefreshAsync(clientId, first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var second = (await refresh.Content.ReadFromJsonAsync<OAuthTokens>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, await AdminPingStatusAsync(second.AccessToken));

        using var reuse = await _signIn.RefreshAsync(clientId, first.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        // The thief's copy and the real one both stop working
        Assert.Equal(HttpStatusCode.Unauthorized, await AdminPingStatusAsync(second.AccessToken));
        using var afterReuse = await _signIn.RefreshAsync(clientId, second.RefreshToken);
        Assert.Equal(HttpStatusCode.BadRequest, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Revoking_a_refresh_token_ends_the_session()
    {
        var tokens = await _signIn.SignInAsync();

        using var revoke = await Http.PostAsync("/oauth/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = tokens.RefreshToken }));

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await AdminPingStatusAsync(tokens.AccessToken));
    }

    [Fact]
    public async Task Panel_and_mcp_tokens_are_not_interchangeable()
    {
        var mcp = await _signIn.SignInAsync();

        // An MCP token on an admin panel controller
        using var panelRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/projects");
        panelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcp.AccessToken);
        using var panelResponse = await Http.SendAsync(panelRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, panelResponse.StatusCode);

        // A panel token on the MCP endpoint
        Assert.Equal(HttpStatusCode.Unauthorized, await AdminPingStatusAsync(AdminPanelToken.Create()));
    }

    [Fact]
    public async Task The_panel_can_revoke_every_session()
    {
        var tokens = await _signIn.SignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/mcp/sessions/revoke");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminPanelToken.Create());
        using var response = await Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await AdminPingStatusAsync(tokens.AccessToken));
    }

    [Fact]
    public async Task The_revoke_all_endpoint_needs_a_panel_login()
    {
        var tokens = await _signIn.SignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/mcp/sessions/revoke");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A bare MCP ping on /mcp/admin, to see whether a token is accepted.</summary>
    private async Task<HttpStatusCode> AdminPingStatusAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AdminMcpRegistration.Endpoint)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Http.SendAsync(request);
        return response.StatusCode;
    }
}
