using Azure;
using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using System.Text.Json;

namespace PortfolioWebsite.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<EmbeddingService> _logger;

    public int Dimensions => 1536;

    public EmbeddingService(IConfiguration configuration, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint must be configured.");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey must be configured.");
        var deployment = configuration["AzureOpenAI:EmbeddingDeployment"]
            ?? "text-embedding-3-small";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _client = azureClient.GetEmbeddingClient(deployment);
    }

    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var truncated = text.Length > 8000 ? text[..8000] : text;

        try
        {
            var result = await _client.GenerateEmbeddingAsync(truncated);
            return result.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for: {Preview}",
                text[..Math.Min(50, text.Length)]);
            return null;
        }
    }

    public float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;

        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denominator = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denominator < 1e-8f ? 0f : dot / denominator;
    }

    public float[]? DeserializeEmbedding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<float[]>(json); }
        catch { return null; }
    }

    public string SerializeEmbedding(float[] embedding)
        => JsonSerializer.Serialize(embedding);
}
