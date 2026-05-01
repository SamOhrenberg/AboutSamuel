using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using System.Text.Json;
using UMAP;

namespace PortfolioWebsite.Api.Services;

public class EmbeddingProjectionService
{
    private readonly IDbContextFactory<SqlDbContext> _dbContextFactory;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<EmbeddingProjectionService> _logger;

    public EmbeddingProjectionService(IDbContextFactory<SqlDbContext> dbContextFactory, IEmbeddingService embeddings, ILogger<EmbeddingProjectionService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _embeddings = embeddings;
        _logger = logger;
    }

    public record ProjectionPoint(
        Guid EntityId,
        string EntityType,
        string Label,
        string? SubLabel,
        float X,
        float Y,
        List<string> TechStack);

    public record QueryProjectionResult(
        float QueryX,
        float QueryY,
        List<NeighborResult> Neighbors);

    public record NeighborResult(
        Guid EntityId,
        string EntityType,
        string Label,
        string? SubLabel,
        float X,
        float Y,
        float Score,
        List<string> TechStack);

    /// <summary>Returns all current projection points.</summary>
    public async Task<List<ProjectionPoint>> GetProjectionsAsync()
    {
        await using var ctx = await _dbContextFactory.CreateDbContextAsync();

        var raw = await ctx.EmbeddingProjections
            .OrderBy(p => p.EntityType)
            .ThenBy(p => p.Label)
            .ToListAsync();

        return raw.Select(p => new ProjectionPoint(
            p.EntityId,
            p.EntityType,
            p.Label,
            p.SubLabel,
            p.X,
            p.Y,
            DeserializeTech(p.TechStack)))
            .ToList();
    }

    /// <summary>
    /// Embeds the query text, finds nearest neighbors via pgvector,
    /// and projects the query into 2D using weighted neighbor averaging.
    /// </summary>
    public async Task<QueryProjectionResult?> ProjectQueryAsync(string queryText, int topN = 5)
    {
        var queryEmbedding = await _embeddings.GetEmbeddingAsync(queryText);
        if (queryEmbedding == null) return null;

        var queryVector = new Vector(queryEmbedding);

        await using var ctx1 = await _dbContextFactory.CreateDbContextAsync();
        await using var ctx2 = await _dbContextFactory.CreateDbContextAsync();
        await using var ctx3 = await _dbContextFactory.CreateDbContextAsync();
        await using var ctx4 = await _dbContextFactory.CreateDbContextAsync();

        // Find top N neighbors from each table using pgvector
        var infoTask = ctx1.Information
            .Where(i => i.Embedding != null)
            .OrderBy(i => i.Embedding!.CosineDistance(queryVector))
            .Take(topN)
            .Select(i => new { i.InformationId, Type = "information", Label = i.Text ?? "", SubLabel = (string?)null, TechStack = "[]", Distance = i.Embedding!.CosineDistance(queryVector) })
            .ToListAsync();

        var projectTask = ctx2.Projects
            .Where(p => p.IsActive && p.Embedding != null)
            .Include(p => p.WorkExperiences)
            .OrderBy(p => p.Embedding!.CosineDistance(queryVector))
            .Take(topN)
            .ToListAsync();

        var workTask = ctx3.WorkExperiences
            .Where(w => w.IsActive && w.Embedding != null)
            .OrderBy(w => w.Embedding!.CosineDistance(queryVector))
            .Take(topN)
            .ToListAsync();

        await Task.WhenAll(infoTask, projectTask, workTask);

        // Build unified candidate list with scores
        var candidates = new List<(Guid Id, string Type, string Label, string? SubLabel, string TechStack, float Score)>();

        foreach (var i in await infoTask)
        {
            candidates.Add((i.InformationId, "information",
                TruncateLabel(i.Label), null, "[]",
                1f - (float)i.Distance));
        }

        foreach (var p in await projectTask)
        {
            var employers = p.WorkExperiences.Select(w => w.Employer).ToList();
            var subLabel = employers.Count > 0 ? string.Join(", ", employers) : null;
            var dist = _embeddings.CosineSimilarity(queryEmbedding, p.Embedding!.ToArray());
            candidates.Add((p.ProjectId, "project", p.Title, subLabel, p.TechStack, dist));
        }

        foreach (var w in await workTask)
        {
            var dist = _embeddings.CosineSimilarity(queryEmbedding, w.Embedding!.ToArray());
            candidates.Add((w.WorkExperienceId, "work", $"{w.Title} at {w.Employer}", w.Employer, "[]", dist));
        }

        // Take overall top N
        var topCandidates = candidates
            .OrderByDescending(c => c.Score)
            .Take(topN)
            .ToList();

        if (topCandidates.Count == 0) return null;

        // Load 2D positions for top candidates
        var topIds = topCandidates.Select(c => c.Id).ToList();
        var projections = await ctx4.EmbeddingProjections
            .Where(p => topIds.Contains(p.EntityId))
            .ToDictionaryAsync(p => p.EntityId);

        // Project query to 2D: weighted average of neighbor positions
        float totalWeight = 0, qx = 0, qy = 0;
        foreach (var c in topCandidates)
        {
            if (!projections.TryGetValue(c.Id, out var proj)) continue;
            var w = c.Score * c.Score; // square the score to emphasize closer neighbors
            qx += proj.X * w;
            qy += proj.Y * w;
            totalWeight += w;
        }

        if (totalWeight > 0) { qx /= totalWeight; qy /= totalWeight; }
        else { qx = 0.5f; qy = 0.5f; }

        // Add a small random jitter so the query point doesn't land exactly on a neighbor
        var rng = new Random();
        qx += (float)(rng.NextDouble() - 0.5) * 0.03f;
        qy += (float)(rng.NextDouble() - 0.5) * 0.03f;
        qx = Math.Clamp(qx, 0.05f, 0.95f);
        qy = Math.Clamp(qy, 0.05f, 0.95f);

        var neighbors = topCandidates.Select(c =>
        {
            projections.TryGetValue(c.Id, out var proj);
            var tech = DeserializeTech(c.TechStack);
            return new NeighborResult(
                c.Id, c.Type, c.Label, c.SubLabel,
                proj?.X ?? qx, proj?.Y ?? qy,
                c.Score, tech);
        }).ToList();

        return new QueryProjectionResult(qx, qy, neighbors);
    }

    /// <summary>
    /// Recomputes UMAP projections for all embedded entities.
    /// Called from the admin panel after generating new embeddings.
    /// </summary>
    public async Task<int> RegenerateProjectionsAsync()
    {
        _logger.LogInformation("Starting UMAP projection regeneration...");

        await using var readCtx = await _dbContextFactory.CreateDbContextAsync();
        await using var writeCtx = await _dbContextFactory.CreateDbContextAsync();

        // Load all embedded entities
        var info = await readCtx.Information
            .Where(i => i.Embedding != null && i.Text != null)
            .ToListAsync();

        var projects = await readCtx.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.IsActive && p.Embedding != null)
            .ToListAsync();

        var work = await readCtx.WorkExperiences
            .Where(w => w.IsActive && w.Embedding != null)
            .ToListAsync();

        // Build unified list of (id, type, label, subLabel, techStack, embedding)
        var items = new List<(Guid Id, string Type, string Label, string? SubLabel, string TechStack, float[] Embedding)>();

        foreach (var i in info)
            items.Add((i.InformationId, "information", TruncateLabel(i.Text!), null, "[]", i.Embedding!.ToArray()));

        foreach (var p in projects)
        {
            var employers = p.WorkExperiences.Select(w => w.Employer).ToList();
            var subLabel = employers.Count > 0 ? string.Join(", ", employers) : null;
            items.Add((p.ProjectId, "project", p.Title, subLabel, p.TechStack, p.Embedding!.ToArray()));
        }

        foreach (var w in work)
            items.Add((w.WorkExperienceId, "work", $"{w.Title}", w.Employer, "[]", w.Embedding!.ToArray()));

        if (items.Count < 4)
        {
            _logger.LogWarning("Not enough embedded items to run UMAP ({Count}). Generate embeddings first.", items.Count);
            return 0;
        }

        _logger.LogInformation("Running UMAP on {Count} points...", items.Count);

        // Build float[][] for UMAP
        var vectors = items.Select(i => i.Embedding).ToArray();

        // UMAP configuration
        var umap = new Umap(
            dimensions: 2,
            numberOfNeighbors: Math.Min(15, items.Count - 1)
        );

        var nEpochs = umap.InitializeFit(vectors);
        for (int i = 0; i < nEpochs; i++)
            umap.Step();

        var embeddings2d = umap.GetEmbedding();

        // Normalize to [0.05, 0.95] so points don't hug the edges
        var xs = embeddings2d.Select(e => e[0]).ToArray();
        var ys = embeddings2d.Select(e => e[1]).ToArray();
        var minX = xs.Min(); var maxX = xs.Max();
        var minY = ys.Min(); var maxY = ys.Max();
        var rangeX = maxX - minX;
        var rangeY = maxY - minY;

        float Norm(float v, float min, float range)
            => range < 1e-6f ? 0.5f : 0.05f + (v - min) / range * 0.90f;

        // Get current version
        var currentVersion = await writeCtx.EmbeddingProjections
            .Select(p => (int?)p.ProjectionVersion)
            .MaxAsync() ?? 0;

        var newVersion = currentVersion + 1;

        // Delete old projections
        await writeCtx.EmbeddingProjections.ExecuteDeleteAsync();

        // Insert new projections
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            writeCtx.EmbeddingProjections.Add(new EmbeddingProjection
            {
                EmbeddingProjectionId = Guid.NewGuid(),
                EntityId = item.Id,
                EntityType = item.Type,
                Label = item.Label,
                SubLabel = item.SubLabel,
                X = Norm(embeddings2d[i][0], minX, rangeX),
                Y = Norm(embeddings2d[i][1], minY, rangeY),
                TechStack = item.TechStack,
                ProjectionVersion = newVersion,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await writeCtx.SaveChangesAsync();
        _logger.LogInformation("UMAP projection complete. {Count} points at version {Version}.", items.Count, newVersion);
        return items.Count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string TruncateLabel(string text)
    {
        if (text.Length <= 60) return text;
        // Try to cut at a sentence boundary
        var idx = text.IndexOf('.', 40);
        if (idx > 0 && idx < 80) return text[..idx];
        return text[..57] + "...";
    }

    private static List<string> DeserializeTech(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
