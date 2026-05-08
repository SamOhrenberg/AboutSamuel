using Serilog.Sinks.Http;
using System.Net.Http.Headers;
using System.Threading;

public class AxiomHttpService : IHttpClient
{
    private readonly HttpClient _httpClient = new();

    public AxiomHttpService(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public void Configure(IConfiguration configuration) { } // no-op

    public void Dispose() => _httpClient.Dispose();

    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Axiom error {(int)response.StatusCode}: {body}");
        }

        return response;
    }
}