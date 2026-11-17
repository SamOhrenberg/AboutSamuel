using Microsoft.Extensions.Options;

namespace PortfolioWebsite.Api.AdminMcp.OAuth;

/// <summary>The authorization server's HTTP surface: metadata, registration, sign-in and tokens.</summary>
public static class AdminOAuthEndpoints
{
    public const string MetadataPath = "/.well-known/oauth-authorization-server";

    public static void MapAdminOAuth(this IEndpointRouteBuilder endpoints)
    {
        // RFC 8414. Clients find this from the issuer in the protected resource metadata.
        endpoints.MapGet(MetadataPath, (IOptions<AdminMcpOptions> options) =>
        {
            var issuer = options.Value.Issuer;
            return Results.Json(new Dictionary<string, object>
            {
                ["issuer"] = issuer,
                ["authorization_endpoint"] = $"{issuer}/oauth/authorize",
                ["token_endpoint"] = $"{issuer}/oauth/token",
                ["registration_endpoint"] = $"{issuer}/oauth/register",
                ["revocation_endpoint"] = $"{issuer}/oauth/revoke",
                ["response_types_supported"] = new[] { "code" },
                ["response_modes_supported"] = new[] { "query" },
                ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["token_endpoint_auth_methods_supported"] = new[] { "none" },
                ["revocation_endpoint_auth_methods_supported"] = new[] { "none" },
                ["scopes_supported"] = new[] { AdminMcpTokens.Scope },
                ["authorization_response_iss_parameter_supported"] = true,
            });
        });

        var oauth = endpoints.MapGroup("/oauth");

        oauth.MapPost("/register", async (ClientRegistrationRequest request, HttpContext http, AdminOAuthService service) =>
        {
            var (client, error) = await service.RegisterAsync(request, Ip(http), http.RequestAborted);
            NoStore(http);
            return error is not null
                ? Results.Json(error, statusCode: error.StatusCode)
                : Results.Json(client, statusCode: StatusCodes.Status201Created);
        }).RequireRateLimiting(AdminMcpRegistration.OAuthRateLimitPolicy);

        oauth.MapGet("/authorize", async (HttpContext http, AdminOAuthService service) =>
        {
            var q = http.Request.Query;
            var request = new AuthorizeRequest(q["response_type"], q["client_id"], q["redirect_uri"], q["state"],
                q["scope"], q["code_challenge"], q["code_challenge_method"], q["resource"]);

            return await service.StartAsync(request, Ip(http), http.RequestAborted) switch
            {
                AuthorizeOutcome.Started started => AdminOAuthPages.Waiting(started),
                AuthorizeOutcome.RedirectError redirect => Results.Redirect(redirect.Url),
                AuthorizeOutcome.ErrorPage page => AdminOAuthPages.Message("Can't sign in", page.Message, 400),
                _ => throw new InvalidOperationException(),
            };
        }).RequireRateLimiting(AdminMcpRegistration.SignInRateLimitPolicy);

        oauth.MapPost("/authorize/poll", async (PollRequest request, HttpContext http, AdminOAuthService service) =>
        {
            NoStore(http);
            return Results.Json(await service.PollAsync(request.Id, request.Key, http.RequestAborted));
        }).RequireRateLimiting(AdminMcpRegistration.PollRateLimitPolicy);

        oauth.MapGet("/approve", async (HttpContext http, AdminOAuthService service) =>
        {
            // GET only shows the page. Mail scanners open links, so approving takes a POST.
            string? token = http.Request.Query["token"];
            var view = await service.GetApprovalAsync(token, http.RequestAborted);
            return view is null
                ? AdminOAuthPages.Message("Link expired", "This approval link is invalid, used, or expired.", 404)
                : AdminOAuthPages.Approval(token!, view);
        }).RequireRateLimiting(AdminMcpRegistration.OAuthRateLimitPolicy);

        oauth.MapPost("/approve", async (HttpContext http, AdminOAuthService service) =>
        {
            if (!http.Request.HasFormContentType)
                return AdminOAuthPages.Message("Can't sign in", "Expected a form.", 400);
            var form = await http.Request.ReadFormAsync(http.RequestAborted);
            string? token = form["token"];
            var approve = form["decision"] == "approve";

            var (outcome, _) = await service.DecideAsync(token, form["code"], approve, http.RequestAborted);
            switch (outcome)
            {
                case DecisionOutcome.Approved:
                    return AdminOAuthPages.Message("Approved",
                        "Go back to the window where you started. It continues by itself.");
                case DecisionOutcome.Denied:
                    return AdminOAuthPages.Message("Denied", "The sign-in was denied. Nothing has access.");
                case DecisionOutcome.LockedOut:
                    return AdminOAuthPages.Message("Denied",
                        "Too many wrong codes, so this sign-in was denied. Start again from the app.", 400);
                case DecisionOutcome.WrongCode:
                    var view = await service.GetApprovalAsync(token, http.RequestAborted);
                    return view is null
                        ? AdminOAuthPages.Message("Link expired", "This approval link is invalid, used, or expired.", 404)
                        : AdminOAuthPages.Approval(token!, view, "That code doesn't match. Check the window where you started.");
                default:
                    return AdminOAuthPages.Message("Link expired", "This approval link is invalid, used, or expired.", 404);
            }
        }).RequireRateLimiting(AdminMcpRegistration.OAuthRateLimitPolicy);

        oauth.MapPost("/token", async (HttpContext http, AdminOAuthService service) =>
        {
            NoStore(http);
            if (!http.Request.HasFormContentType)
                return Results.Json(new OAuthError("invalid_request", "Send the token request as a form."), statusCode: 400);
            var form = await http.Request.ReadFormAsync(http.RequestAborted);
            string? clientId = form["client_id"];

            var (tokens, error) = form["grant_type"].ToString() switch
            {
                "authorization_code" => await service.ExchangeCodeAsync(clientId, form["code"], form["redirect_uri"],
                    form["code_verifier"], form["resource"], http.RequestAborted),
                "refresh_token" => await service.RefreshAsync(clientId, form["refresh_token"], http.RequestAborted),
                _ => (null, new OAuthError("unsupported_grant_type", "Use authorization_code or refresh_token.")),
            };
            return error is not null ? Results.Json(error, statusCode: error.StatusCode) : Results.Json(tokens);
        }).RequireRateLimiting(AdminMcpRegistration.OAuthRateLimitPolicy);

        oauth.MapPost("/revoke", async (HttpContext http, AdminOAuthService service) =>
        {
            if (http.Request.HasFormContentType)
            {
                var form = await http.Request.ReadFormAsync(http.RequestAborted);
                await service.RevokeAsync(form["token"], http.RequestAborted);
            }
            return Results.Ok();  // RFC 7009: the same answer whether or not the token existed
        }).RequireRateLimiting(AdminMcpRegistration.OAuthRateLimitPolicy);
    }

    private static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static void NoStore(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
    }
}
