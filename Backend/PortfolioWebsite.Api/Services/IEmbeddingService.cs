namespace PortfolioWebsite.Api.Services;

public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// Returns null if the call fails.
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string text);

    /// <summary>
    /// Embeds several texts in one API call. The result lines up with the input.
    /// Returns null if the call fails or any text is blank.
    /// </summary>
    Task<IReadOnlyList<float[]>?> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of dimensions in the embedding vector.
    /// text-embedding-3-small = 1536
    /// </summary>
    int Dimensions { get; }

    float CosineSimilarity(float[] a, float[] b);
    float[]? DeserializeEmbedding(string? json);
    string SerializeEmbedding(float[] embedding);
}
