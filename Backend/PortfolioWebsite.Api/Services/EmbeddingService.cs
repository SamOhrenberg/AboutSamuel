using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text.Json;

namespace PortfolioWebsite.Api.Services;

public class EmbeddingService
{
    private const string ModelId = "amazon.titan-embed-text-v2:0";

    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IAmazonBedrockRuntime bedrockClient,
        ILogger<EmbeddingService> logger)
    {
        _bedrockClient = bedrockClient;
        _logger = logger;
    }

    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Titan has an 8192 token limit — truncate to stay safe
        var truncated = text.Length > 4000 ? text[..4000] : text;

        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new { inputText = truncated });

            var response = await _bedrockClient.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = ModelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(body)
            });

            using var doc = JsonDocument.Parse(response.Body);
            return doc.RootElement
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedding for: {Preview}",
                text[..Math.Min(50, text.Length)]);
            return null;
        }
    }

    public static float CosineSimilarity(float[] a, float[] b)
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

    public static float[]? DeserializeEmbedding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<float[]>(json); }
        catch { return null; }
    }

    public static string SerializeEmbedding(float[] embedding)
        => JsonSerializer.Serialize(embedding);
}