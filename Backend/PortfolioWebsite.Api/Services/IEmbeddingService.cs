namespace PortfolioWebsite.Api.Services;

public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// Returns null if the call fails.
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string text);

    /// <summary>
    /// The number of dimensions in the embedding vector.
    /// text-embedding-3-small = 1536
    /// </summary>
    int Dimensions { get; }

    float CosineSimilarity(float[] a, float[] b);
    float[]? DeserializeEmbedding(string? json);
    string SerializeEmbedding(float[] embedding);
}
