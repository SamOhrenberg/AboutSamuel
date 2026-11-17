using Microsoft.EntityFrameworkCore;
using Pgvector;
using PortfolioWebsite.Api.Data;

namespace PortfolioWebsite.Api.Services.Admin;

public record EmbeddingBatchResult(int Success, int Failed, int Total);

/// <summary>
/// Keeps the RAG embeddings in step with the text. Edits clear an item's embedding; these
/// rebuild them, one item or everything that's missing.
/// </summary>
public class AdminEmbeddingService(
    SqlDbContext _db,
    IEmbeddingService _embeddings,
    EmbeddingProjectionService _projections,
    ILogger<AdminEmbeddingService> _logger)
{
    public static readonly string[] ItemTypes = ["information", "project", "work-experience"];

    /// <summary>Embeds every information entry, active project and active job without one.</summary>
    public async Task<EmbeddingBatchResult> GenerateMissingAsync(CancellationToken ct = default)
    {
        var infoEntries = await _db.Information
            .Where(i => i.Embedding == null && i.Text != null)
            .ToListAsync(ct);
        var projects = await _db.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.Embedding == null && p.IsActive)
            .ToListAsync(ct);
        var workEntries = await _db.WorkExperiences
            .Where(w => w.Embedding == null && w.IsActive)
            .ToListAsync(ct);

        var jobs = new List<(string Text, Action<Vector> Persist)>();
        jobs.AddRange(infoEntries.Select(e => (e.Text!, (Action<Vector>)(v => e.Embedding = v))));
        jobs.AddRange(projects.Select(p => (ChatService.BuildProjectRagText(p), (Action<Vector>)(v => p.Embedding = v))));
        jobs.AddRange(workEntries.Select(w => (ChatService.BuildWorkRagText(w), (Action<Vector>)(v => w.Embedding = v))));

        int success = 0, failed = 0;
        foreach (var (text, persist) in jobs)
        {
            var embedding = await _embeddings.GetEmbeddingAsync(text);
            if (embedding != null)
            {
                persist(new Vector(embedding));
                success++;
            }
            else failed++;
            await Task.Delay(50, ct);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Embedding generation complete: {Success} succeeded, {Failed} failed", success, failed);
        return new EmbeddingBatchResult(success, failed, jobs.Count);
    }

    /// <summary>Rebuilds one item's embedding. type is information, project or work-experience.</summary>
    public async Task<AdminResult> GenerateForItemAsync(string type, Guid id, CancellationToken ct = default)
    {
        string? text;
        Action<Vector> persist;

        switch (type.ToLowerInvariant())
        {
            case "information":
                {
                    var entry = await _db.Information.FindAsync([id], ct);
                    if (entry is null) return AdminFailure.NotFound();
                    text = entry.Text;
                    persist = v => entry.Embedding = v;
                    break;
                }
            case "project":
                {
                    var project = await _db.Projects
                        .Include(p => p.WorkExperiences)
                        .FirstOrDefaultAsync(p => p.ProjectId == id, ct);
                    if (project is null) return AdminFailure.NotFound();
                    text = ChatService.BuildProjectRagText(project);
                    persist = v => project.Embedding = v;
                    break;
                }
            case "work-experience":
                {
                    var job = await _db.WorkExperiences.FindAsync([id], ct);
                    if (job is null) return AdminFailure.NotFound();
                    text = ChatService.BuildWorkRagText(job);
                    persist = v => job.Embedding = v;
                    break;
                }
            default:
                return AdminFailure.Invalid("type must be 'information', 'project', or 'work-experience'");
        }

        if (string.IsNullOrWhiteSpace(text))
            return AdminFailure.Invalid("Entry has no text to embed.");

        var embedding = await _embeddings.GetEmbeddingAsync(text);
        if (embedding is null)
            return AdminFailure.Unavailable("Embedding service failed.");

        persist(new Vector(embedding));
        await _db.SaveChangesAsync(ct);
        return AdminResult.Ok();
    }

    /// <summary>Recomputes the skill map (UMAP projection). Returns the number of points.</summary>
    public async Task<AdminResult<int>> RegenerateSkillMapAsync()
    {
        _logger.LogInformation("Admin triggered UMAP projection regeneration.");
        int count;
        try
        {
            count = await _projections.RegenerateProjectionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating visualization.");
            return AdminFailure.Failed("Regeneration failed. Check logs.");
        }

        return count == 0
            ? AdminFailure.Invalid("No embedded items found. Run Generate Embeddings first.")
            : count;
    }
}
