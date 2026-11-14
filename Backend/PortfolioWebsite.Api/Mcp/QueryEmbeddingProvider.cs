using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PortfolioWebsite.Api.Services;

namespace PortfolioWebsite.Api.Mcp;

public sealed class SemanticSearchOptions
{
    public const string SectionName = "Mcp:SemanticSearch";

    /// <summary>Off means every search uses Postgres full-text and never calls Azure OpenAI.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Embedding API calls per client IP per hour. A find_evidence batch counts once.
    /// Cache hits don't count. Generous because hosted AI clients share egress IPs across
    /// users; it only stops one caller from spending the whole DailyCap.</summary>
    public int PerIpPerHour { get; set; } = 300;

    /// <summary>Texts embedded per UTC day across all clients. This is the cost ceiling.</summary>
    public int DailyCap { get; set; } = 2000;

    /// <summary>How long a query's vector is reused. 0 turns the cache off.</summary>
    public int CacheMinutes { get; set; } = 60;

    public int CacheMaxEntries { get; set; } = 1000;
}

/// <summary>Query vectors for the public search tools, or null to tell the caller to use full-text.</summary>
public interface IQueryEmbeddingProvider
{
    /// <summary>
    /// One vector per text, in order, or null when semantic search isn't available for this
    /// call: the flag is off, the caller's IP or the daily cap is out of budget, or the
    /// embedding call failed. All or nothing, so a caller never mixes modes.
    /// </summary>
    Task<IReadOnlyList<float[]>?> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Wraps IEmbeddingService for anonymous MCP traffic, so the only spend is query embeddings
/// and it's bounded. Order: flag, cache, per-IP limiter, global daily cap, then one batched
/// call for whatever wasn't cached. The limiter and the daily counter live in memory, which
/// is exact for the single Railway instance; with more replicas each would get its own.
/// </summary>
public sealed partial class QueryEmbeddingProvider : IQueryEmbeddingProvider, IDisposable
{
    private readonly IEmbeddingService _embeddings;
    private readonly SemanticSearchOptions _options;
    private readonly IHttpContextAccessor _http;
    private readonly TimeProvider _time;
    private readonly ILogger<QueryEmbeddingProvider> _logger;
    private readonly MemoryCache _cache;
    private readonly PartitionedRateLimiter<string> _perIp;

    private readonly Lock _dailyLock = new();
    private DateOnly _day;
    private int _usedToday;

    public QueryEmbeddingProvider(
        IEmbeddingService embeddings,
        IOptions<SemanticSearchOptions> options,
        IHttpContextAccessor http,
        TimeProvider time,
        ILogger<QueryEmbeddingProvider> logger)
    {
        _embeddings = embeddings;
        _options = options.Value;
        _http = http;
        _time = time;
        _logger = logger;

        // Its own cache, so the size limit doesn't force sizes onto anything else's entries.
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = Math.Max(1, _options.CacheMaxEntries) });
        _perIp = PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, _options.PerIpPerHour),
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public async Task<IReadOnlyList<float[]>?> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
            return [];
        if (!_options.Enabled)
            return Fallback("semantic search is disabled");

        var normalized = texts.Select(Normalize).ToList();
        if (normalized.Any(t => t.Length == 0))
            return Fallback("a query was blank");

        var keys = normalized.Select(CacheKey).ToList();
        var vectors = new float[]?[texts.Count];
        var missing = new Dictionary<string, string>();
        for (var i = 0; i < keys.Count; i++)
        {
            if (_cache.TryGetValue(keys[i], out float[]? cached) && cached is not null)
                vectors[i] = cached;
            else
                missing.TryAdd(keys[i], normalized[i]);
        }

        if (missing.Count > 0)
        {
            if (!TryAcquireIpPermit())
                return Fallback("the per-IP hourly limit was reached");
            if (!TryReserveDaily(missing.Count))
                return Fallback("the daily embedding cap was reached");

            var embedded = await _embeddings.GetEmbeddingsAsync(missing.Values.ToList(), cancellationToken);
            if (embedded is null || embedded.Count != missing.Count)
                return Fallback("the embedding call failed");

            var byKey = missing.Keys.Zip(embedded).ToDictionary(p => p.First, p => p.Second);
            if (_options.CacheMinutes > 0)
            {
                var entry = new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes),
                };
                foreach (var (key, vector) in byKey)
                    _cache.Set(key, vector, entry);
            }

            for (var i = 0; i < keys.Count; i++)
                vectors[i] ??= byKey[keys[i]];
        }

        return vectors.Select(v => v!).ToList();
    }

    /// <summary>Trimmed, whitespace collapsed, NFC. Both the cache key and the embedded text.</summary>
    public static string Normalize(string? text) =>
        Whitespace().Replace((text ?? "").Normalize(NormalizationForm.FormC), " ").Trim();

    private static string CacheKey(string normalized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

    private bool TryAcquireIpPermit()
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        using var lease = _perIp.AttemptAcquire(ip);
        return lease.IsAcquired;
    }

    private bool TryReserveDaily(int count)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        lock (_dailyLock)
        {
            if (today != _day)
            {
                _day = today;
                _usedToday = 0;
            }
            if (_usedToday + count > _options.DailyCap)
                return false;
            _usedToday += count;
            return true;
        }
    }

    private IReadOnlyList<float[]>? Fallback(string reason)
    {
        // No query text or IP here: the reason is enough to explain a fulltext mode.
        _logger.LogInformation("MCP search is using full-text because {Reason}", reason);
        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public void Dispose()
    {
        _cache.Dispose();
        _perIp.Dispose();
    }
}
