using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// Limits on /mcp itself. Hosts with non-default settings come from WithWebHostBuilder:
/// same container, own host, so nothing test-only lives in the shared one.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class McpHardeningTests(ApiFactory factory)
{
    private const int TestPermitLimit = 3;

    private static StringContent Ping() => new(
        """{"jsonrpc":"2.0","id":1,"method":"ping"}""", Encoding.UTF8, "application/json");

    private static HttpRequestMessage Post(HttpContent content, string? clientIp = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, McpRegistration.Endpoint) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        // ForwardedHeaders trusts any proxy (Railway), so this sets RemoteIpAddress.
        if (clientIp is not null)
            request.Headers.Add("X-Forwarded-For", clientIp);
        return request;
    }

    [Fact]
    public async Task Requests_over_the_limit_get_429_per_client_ip()
    {
        await using var limited = factory.WithWebHostBuilder(b =>
            b.UseSetting("Mcp:RateLimit:PermitLimit", TestPermitLimit.ToString()));
        using var http = limited.CreateClient();

        for (var i = 0; i < TestPermitLimit; i++)
        {
            using var ok = await http.SendAsync(Post(Ping(), "203.0.113.10"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var rejected = await http.SendAsync(Post(Ping(), "203.0.113.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        using var otherClient = await http.SendAsync(Post(Ping(), "203.0.113.11"));
        Assert.Equal(HttpStatusCode.OK, otherClient.StatusCode);
    }

    [Fact]
    public async Task Unexpected_failures_reach_the_client_without_details_and_are_logged_server_side()
    {
        var logs = new LogCapture();
        await using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPortfolioReadService>();
            services.AddScoped<IPortfolioReadService, ThrowingReadService>();
            services.AddSingleton<ILoggerProvider>(logs);
        }));
        await using var client = await McpTestClient.ConnectAsync(broken);

        var text = await client.CallExpectingErrorAsync("get_project", new() { ["slugOrId"] = "portfolio-chat" });

        // The SDK's generic message; nothing from the exception.
        Assert.Equal("An error occurred invoking 'get_project'.", text);
        Assert.DoesNotContain(ThrowingReadService.Canary, text);
        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain(" at ", text);

        // A real fault isn't an McpException, so the client-error log filter keeps it.
        var logged = Assert.Single(logs.Lines, l => l.Exception is InvalidOperationException);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains(ThrowingReadService.Canary, logged.Exception!.Message);
    }

    private sealed class ThrowingReadService : IPortfolioReadService
    {
        public const string Canary = "connection to db-internal.railway failed";

        private static T Fail<T>() =>
            throw new InvalidOperationException(Canary, new TimeoutException("inner " + Canary));

        public Task<PagedResult<ProjectSummary>> ListProjectsAsync(ProjectQuery query, CancellationToken ct = default) => Fail<Task<PagedResult<ProjectSummary>>>();
        public Task<ProjectDetail?> GetProjectAsync(string slugOrId, CancellationToken ct = default) => Fail<Task<ProjectDetail?>>();
        public Task<IReadOnlyList<SkillSummary>> GetSkillsAsync(int minProjects = 1, CancellationToken ct = default) => Fail<Task<IReadOnlyList<SkillSummary>>>();
        public Task<IReadOnlyList<ExperienceItem>> GetExperienceAsync(CancellationToken ct = default) => Fail<Task<IReadOnlyList<ExperienceItem>>>();
    }

    [Fact]
    public async Task Oversized_body_is_rejected()
    {
        var padding = new string('a', (int)McpRegistration.MaxRequestBodyBytes);
        var body = """{"jsonrpc":"2.0","id":1,"method":"ping","params":{"_meta":{"pad":"PAD"}}}""".Replace("PAD", padding);
        using var http = factory.CreateClient();

        using var response = await http.SendAsync(Post(new StringContent(body, Encoding.UTF8, "application/json")));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Body_just_under_the_cap_is_accepted()
    {
        var envelope = """{"jsonrpc":"2.0","id":1,"method":"ping","params":{"_meta":{"pad":""}}}""";
        var padding = new string('a', (int)McpRegistration.MaxRequestBodyBytes - envelope.Length);
        var body = envelope.Replace("\"pad\":\"\"", $"\"pad\":\"{padding}\"");
        Assert.Equal(McpRegistration.MaxRequestBodyBytes, Encoding.UTF8.GetByteCount(body));
        using var http = factory.CreateClient();

        using var response = await http.SendAsync(Post(new StringContent(body, Encoding.UTF8, "application/json")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
