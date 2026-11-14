using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class QueryEmbeddingProviderTests
{
    private readonly RecordingEmbeddingService _embeddings = new();
    private readonly HttpContextAccessor _http = new() { HttpContext = ContextFor("203.0.113.1") };
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 23, 0, 0, TimeSpan.Zero));

    private QueryEmbeddingProvider Provider(Action<SemanticSearchOptions>? configure = null)
    {
        var options = new SemanticSearchOptions { PerIpPerHour = 100, DailyCap = 100 };
        configure?.Invoke(options);
        return new QueryEmbeddingProvider(_embeddings, Options.Create(options), _http, _time,
            NullLogger<QueryEmbeddingProvider>.Instance);
    }

    [Fact]
    public async Task Returns_one_vector_per_text_in_order()
    {
        using var provider = Provider();

        var vectors = await provider.EmbedAsync(["alpha", "beta"]);

        Assert.NotNull(vectors);
        Assert.Equal([RecordingEmbeddingService.VectorFor("alpha"), RecordingEmbeddingService.VectorFor("beta")], vectors);
        Assert.Equal([["alpha", "beta"]], _embeddings.Batches);
    }

    [Fact]
    public async Task A_cache_hit_avoids_the_embedding_call()
    {
        using var provider = Provider();

        await provider.EmbedAsync(["hello world"]);
        var second = await provider.EmbedAsync(["  hello \n world "]);

        Assert.NotNull(second);
        Assert.Single(_embeddings.Batches);
    }

    [Fact]
    public async Task Only_uncached_texts_are_sent_and_duplicates_once()
    {
        using var provider = Provider();

        await provider.EmbedAsync(["a"]);
        var vectors = await provider.EmbedAsync(["a", "b", "b"]);

        Assert.Equal([["a"], ["b"]], _embeddings.Batches);
        Assert.NotNull(vectors);
        Assert.Equal(3, vectors.Count);
        Assert.Equal(RecordingEmbeddingService.VectorFor("b"), vectors[2]);
    }

    [Fact]
    public async Task Flag_off_falls_back_without_calling_the_api()
    {
        using var provider = Provider(o => o.Enabled = false);

        Assert.Null(await provider.EmbedAsync(["anything"]));
        Assert.Empty(_embeddings.Batches);
    }

    [Fact]
    public async Task Per_ip_limit_falls_back_for_that_ip_only()
    {
        using var provider = Provider(o => o.PerIpPerHour = 2);

        Assert.NotNull(await provider.EmbedAsync(["one"]));
        Assert.NotNull(await provider.EmbedAsync(["two"]));
        Assert.Null(await provider.EmbedAsync(["three"]));

        _http.HttpContext = ContextFor("198.51.100.7");
        Assert.NotNull(await provider.EmbedAsync(["three"]));
    }

    [Fact]
    public async Task A_fully_cached_call_does_not_spend_a_permit()
    {
        using var provider = Provider(o => o.PerIpPerHour = 1);

        Assert.NotNull(await provider.EmbedAsync(["cached"]));
        Assert.NotNull(await provider.EmbedAsync(["cached"]));
        Assert.Null(await provider.EmbedAsync(["new"]));
    }

    [Fact]
    public async Task Daily_cap_counts_texts_and_resets_at_utc_midnight()
    {
        using var provider = Provider(o => o.DailyCap = 3);

        Assert.NotNull(await provider.EmbedAsync(["a", "b"]));
        Assert.Null(await provider.EmbedAsync(["c", "d"]));
        Assert.NotNull(await provider.EmbedAsync(["c"]));
        Assert.Null(await provider.EmbedAsync(["d"]));

        _time.Advance(TimeSpan.FromHours(1));
        Assert.NotNull(await provider.EmbedAsync(["d"]));
    }

    [Fact]
    public async Task A_failed_call_falls_back_and_is_not_cached()
    {
        using var provider = Provider();
        _embeddings.Fail = true;

        Assert.Null(await provider.EmbedAsync(["flaky"]));

        _embeddings.Fail = false;
        Assert.NotNull(await provider.EmbedAsync(["flaky"]));
        Assert.Equal(2, _embeddings.Batches.Count);
    }

    [Fact]
    public async Task Cache_minutes_zero_turns_the_cache_off()
    {
        using var provider = Provider(o => o.CacheMinutes = 0);

        await provider.EmbedAsync(["again"]);
        await provider.EmbedAsync(["again"]);

        Assert.Equal(2, _embeddings.Batches.Count);
    }

    [Fact]
    public async Task Blank_text_falls_back_and_an_empty_list_is_empty()
    {
        using var provider = Provider();

        Assert.Null(await provider.EmbedAsync(["fine", "   "]));
        Assert.Empty((await provider.EmbedAsync([]))!);
        Assert.Empty(_embeddings.Batches);
    }

    [Fact]
    public void Normalize_trims_and_collapses_whitespace()
    {
        Assert.Equal("a b c", QueryEmbeddingProvider.Normalize("  a\t b\r\n\nc "));
        Assert.Equal("", QueryEmbeddingProvider.Normalize(null));
    }

    private static DefaultHttpContext ContextFor(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public List<List<string>> Batches { get; } = [];
        public bool Fail { get; set; }
        public int Dimensions => 3;

        public static float[] VectorFor(string text) => [text.Length, text[0], 1];

        public Task<IReadOnlyList<float[]>?> GetEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            Batches.Add([.. texts]);
            return Task.FromResult(Fail ? null : (IReadOnlyList<float[]>?)texts.Select(VectorFor).ToList());
        }

        public Task<float[]?> GetEmbeddingAsync(string text) => throw new NotSupportedException("Use the batch method.");
        public float CosineSimilarity(float[] a, float[] b) => throw new NotSupportedException();
        public float[]? DeserializeEmbedding(string? json) => throw new NotSupportedException();
        public string SerializeEmbedding(float[] embedding) => throw new NotSupportedException();
    }
}
