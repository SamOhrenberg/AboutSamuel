using Azure.AI.OpenAI;
using OpenAI.Embeddings;
using System.Text.Json;

namespace PortfolioWebsite.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<EmbeddingService> _logger;

    public int Dimensions => 1536;

    public EmbeddingService(AzureOpenAIClient azureClient, IConfiguration configuration, ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        var deployment = configuration["AzureOpenAI:EmbeddingDeployment"]
            ?? "embedding";

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

    public async Task<IReadOnlyList<float[]>?> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];
        if (texts.Any(string.IsNullOrWhiteSpace)) return null;

        var truncated = texts.Select(t => t.Length > 8000 ? t[..8000] : t).ToList();

        try
        {
            var result = await _client.GenerateEmbeddingsAsync(truncated, cancellationToken: cancellationToken);
            var vectors = result.Value.OrderBy(e => e.Index).Select(e => e.ToFloats().ToArray()).ToList();
            if (vectors.Count != texts.Count)
            {
                _logger.LogError("Embedding batch returned {Returned} vectors for {Requested} texts",
                    vectors.Count, texts.Count);
                return null;
            }
            return vectors;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate a batch of {Count} embeddings", texts.Count);
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
