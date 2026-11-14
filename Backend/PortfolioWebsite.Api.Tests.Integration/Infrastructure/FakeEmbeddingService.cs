using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Deterministic stand-in for Azure OpenAI embeddings: the same text always yields the same
/// unit-length vector, seeded from its SHA-256. Counts calls so tests can assert on caching.
/// </summary>
public sealed class FakeEmbeddingService : IEmbeddingService
{
    private int _calls;

    public int Dimensions => 1536;

    public int Calls => Volatile.Read(ref _calls);

    public Task<float[]?> GetEmbeddingAsync(string text)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<float[]?>(VectorFor(text));
    }

    public float[] VectorFor(string text)
    {
        var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var random = new Random(seed);
        var vector = new float[Dimensions];
        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(random.NextDouble() * 2 - 1);

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
        return vector;
    }

    public float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    public float[]? DeserializeEmbedding(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<float[]>(json);

    public string SerializeEmbedding(float[] embedding) => JsonSerializer.Serialize(embedding);
}
