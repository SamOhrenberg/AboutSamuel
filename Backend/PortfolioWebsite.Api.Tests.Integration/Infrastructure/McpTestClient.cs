using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>A real McpClient over Streamable HTTP to /mcp on the in-memory host.</summary>
public static class McpTestClient
{
    /// <param name="clientIp">Sent as X-Forwarded-For, which the API trusts, so it becomes
    /// RemoteIpAddress. Without it every test request shares the "unknown" partition.</param>
    public static Task<McpClient> ConnectAsync(WebApplicationFactory<Program> factory, string? clientIp = null)
    {
        var http = factory.CreateClient();
        if (clientIp is not null)
            http.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, McpRegistration.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http, NullLoggerFactory.Instance, ownsHttpClient: true);
        return McpClient.CreateAsync(transport);
    }

    /// <summary>Calls a tool, asserts it succeeded, and reads its structured content.</summary>
    public static async Task<T> CallStructuredAsync<T>(this McpClient client, string tool,
        Dictionary<string, object?>? args = null)
    {
        var result = await client.CallToolAsync(tool, args);
        Assert.True(result.IsError is not true, $"{tool} failed: {TextOf(result)}");
        return JsonSerializer.SerializeToElement(result.StructuredContent).Deserialize<T>(JsonSerializerOptions.Web)!;
    }

    /// <summary>Calls a tool, asserts it failed, and returns the error text.</summary>
    public static async Task<string> CallExpectingErrorAsync(this McpClient client, string tool,
        Dictionary<string, object?> args)
    {
        var result = await client.CallToolAsync(tool, args);
        Assert.True(result.IsError, $"{tool} should have failed");
        return TextOf(result);
    }

    public static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}
