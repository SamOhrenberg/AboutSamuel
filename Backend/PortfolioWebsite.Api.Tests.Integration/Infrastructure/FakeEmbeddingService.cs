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
    private int _batchCalls;

    public int Dimensions => 1536;

    public int Calls => Volatile.Read(ref _calls);

    public int BatchCalls => Volatile.Read(ref _batchCalls);

    /// <summary>When true, every call fails the way the real service does: it returns null.</summary>
    public bool Fail { get; set; }

    public Task<float[]?> GetEmbeddingAsync(string text)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Fail ? null : (float[]?)VectorFor(text));
    }

    public Task<IReadOnlyList<float[]>?> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _batchCalls);
        return Task.FromResult(Fail ? null : (IReadOnlyList<float[]>?)texts.Select(VectorFor).ToList());
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
