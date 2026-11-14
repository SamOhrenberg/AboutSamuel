using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Search over the public portfolio (active projects, active work experience, and notes).
/// Separate from IPortfolioReadService because it also depends on query embeddings.
/// Inputs are expected to be validated already (see PortfolioSearchTools).
/// </summary>
public interface IPortfolioSearchService
{
    Task<SearchResult> SearchAsync(string query, int limit, IReadOnlyCollection<string> types,
        CancellationToken cancellationToken = default);

    Task<EvidenceResult> FindEvidenceAsync(IReadOnlyList<string> requirements, int perRequirement,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Semantic search with pgvector cosine distance when a query vector is available (the same
/// three tables the chat's RAG uses), otherwise Postgres full-text ranked with ts_rank,
/// computed on the fly because the tables are small. Each table returns its own top N and
/// the merged list is cut to N.
/// </summary>
public sealed class PortfolioSearchService(
    IDbContextFactory<PublicPortfolioDbContext> dbFactory,
    IQueryEmbeddingProvider embeddings,
    PortfolioUrls urls,
    ILogger<PortfolioSearchService> logger) : IPortfolioSearchService
{
    private const string Config = "english";

    public async Task<SearchResult> SearchAsync(string query, int limit, IReadOnlyCollection<string> types,
        CancellationToken cancellationToken = default)
    {
        var vectors = await embeddings.EmbedAsync([query], cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var slugs = await LoadSlugsAsync(db, cancellationToken);
        var hits = await SearchOneAsync(db, slugs, query, vectors?[0], limit, types, cancellationToken);

        return new SearchResult(query, Mode(vectors), hits, hits.Count);
    }

    public async Task<EvidenceResult> FindEvidenceAsync(IReadOnlyList<string> requirements, int perRequirement,
        CancellationToken cancellationToken = default)
    {
        // One batched embedding call for the whole list (minus cache hits).
        var vectors = await embeddings.EmbedAsync(requirements, cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var slugs = await LoadSlugsAsync(db, cancellationToken);

        var results = new List<RequirementEvidence>(requirements.Count);
        for (var i = 0; i < requirements.Count; i++)
        {
            var matches = await SearchOneAsync(db, slugs, requirements[i], vectors?[i], perRequirement,
                SearchTypes.All, cancellationToken);
            results.Add(new RequirementEvidence(requirements[i], matches));
        }

        return new EvidenceResult(Mode(vectors), results);
    }

    private static string Mode(IReadOnlyList<float[]>? vectors) =>
        vectors is null ? SearchModes.FullText : SearchModes.Semantic;

    private async Task<IReadOnlyList<SearchHit>> SearchOneAsync(
        PublicPortfolioDbContext db, IReadOnlyDictionary<Guid, string> slugs,
        string query, float[]? vector, int limit, IReadOnlyCollection<string> types,
        CancellationToken cancellationToken)
    {
        string? tsQuery = null;
        if (vector is null)
        {
            tsQuery = SearchText.ToFullTextQuery(query);
            if (tsQuery is null)
                return [];
        }

        var hits = new List<SearchHit>();
        var v = vector is null ? null : new Vector(vector);

        if (types.Contains(SearchTypes.Project))
            hits.AddRange(v is not null
                ? await SemanticProjectsAsync(db, slugs, v, limit, cancellationToken)
                : await FullTextProjectsAsync(db, slugs, tsQuery!, limit, cancellationToken));
        if (types.Contains(SearchTypes.Experience))
            hits.AddRange(v is not null
                ? await SemanticExperienceAsync(db, v, limit, cancellationToken)
                : await FullTextExperienceAsync(db, tsQuery!, limit, cancellationToken));
        if (types.Contains(SearchTypes.Note))
            hits.AddRange(v is not null
                ? await SemanticNotesAsync(db, v, limit, cancellationToken)
                : await FullTextNotesAsync(db, tsQuery!, limit, cancellationToken));

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => SearchTypes.All.ToList().IndexOf(h.Type))
            .ThenBy(h => h.Id)
            .Take(limit)
            .ToList();
    }

    // Projects

    private async Task<List<SearchHit>> SemanticProjectsAsync(PublicPortfolioDbContext db,
        IReadOnlyDictionary<Guid, string> slugs, Vector v, int limit, CancellationToken ct)
    {
        var rows = await db.Projects
            .Where(p => p.IsActive && p.Embedding != null)
            .Select(p => new { p.ProjectId, p.Title, p.Summary, p.ImpactStatement, Distance = p.Embedding!.CosineDistance(v) })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => ProjectHit(slugs, r.ProjectId, r.Title, r.Summary, r.ImpactStatement, 1 - r.Distance)).ToList();
    }

    private async Task<List<SearchHit>> FullTextProjectsAsync(PublicPortfolioDbContext db,
        IReadOnlyDictionary<Guid, string> slugs, string tsQuery, int limit, CancellationToken ct)
    {
        var rows = await db.Projects
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.ProjectId, p.Title, p.Summary, p.ImpactStatement,
                Document = EF.Functions.ToTsVector(Config,
                    p.Title + " " + p.Role + " " + p.Summary + " " + (p.Detail ?? "") + " " +
                    (p.ImpactStatement ?? "") + " " + p.TechStack),
            })
            .Where(x => x.Document.Matches(EF.Functions.WebSearchToTsQuery(Config, tsQuery)))
            .Select(x => new { x.ProjectId, x.Title, x.Summary, x.ImpactStatement,
                Rank = x.Document.Rank(EF.Functions.WebSearchToTsQuery(Config, tsQuery)) })
            .OrderByDescending(x => x.Rank)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => ProjectHit(slugs, r.ProjectId, r.Title, r.Summary, r.ImpactStatement, r.Rank)).ToList();
    }

    private SearchHit ProjectHit(IReadOnlyDictionary<Guid, string> slugs, Guid id, string title,
        string summary, string? impact, double score) =>
        new(SearchTypes.Project, id, slugs.GetValueOrDefault(id), title,
            SearchText.Snippet(string.Join(" ", new[] { summary, impact }.Where(s => !string.IsNullOrWhiteSpace(s)))),
            Round(score), urls.Project(id));

    // Work experience

    private async Task<List<SearchHit>> SemanticExperienceAsync(PublicPortfolioDbContext db, Vector v, int limit,
        CancellationToken ct)
    {
        var rows = await db.WorkExperiences
            .Where(w => w.IsActive && w.Embedding != null)
            .Select(w => new { w.WorkExperienceId, w.Employer, w.Title, w.Summary, w.Achievements,
                Distance = w.Embedding!.CosineDistance(v) })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => ExperienceHit(r.WorkExperienceId, r.Employer, r.Title, r.Summary, r.Achievements,
            1 - r.Distance)).ToList();
    }

    private async Task<List<SearchHit>> FullTextExperienceAsync(PublicPortfolioDbContext db, string tsQuery, int limit,
        CancellationToken ct)
    {
        var rows = await db.WorkExperiences
            .Where(w => w.IsActive)
            .Select(w => new
            {
                w.WorkExperienceId, w.Employer, w.Title, w.Summary, w.Achievements,
                Document = EF.Functions.ToTsVector(Config,
                    w.Employer + " " + w.Title + " " + (w.Summary ?? "") + " " + w.Achievements),
            })
            .Where(x => x.Document.Matches(EF.Functions.WebSearchToTsQuery(Config, tsQuery)))
            .Select(x => new { x.WorkExperienceId, x.Employer, x.Title, x.Summary, x.Achievements,
                Rank = x.Document.Rank(EF.Functions.WebSearchToTsQuery(Config, tsQuery)) })
            .OrderByDescending(x => x.Rank)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => ExperienceHit(r.WorkExperienceId, r.Employer, r.Title, r.Summary, r.Achievements,
            r.Rank)).ToList();
    }

    private SearchHit ExperienceHit(Guid id, string employer, string title, string? summary, string achievements,
        double score)
    {
        if (!JsonStringArray.TryParse(achievements, out var parsed))
            logger.LogWarning("WorkExperience {Id} has malformed JSON in Achievements; leaving it out of the snippet", id);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary.Trim());
        parts.AddRange(parsed);

        return new SearchHit(SearchTypes.Experience, id, null, $"{title} at {employer}",
            SearchText.Snippet(string.Join(" ", parts)), Round(score), urls.Experience(id));
    }

    // Notes (the Information table: free-text knowledge Samuel wrote for the chat)

    private async Task<List<SearchHit>> SemanticNotesAsync(PublicPortfolioDbContext db, Vector v, int limit,
        CancellationToken ct)
    {
        var rows = await db.Information
            .Where(i => i.Text != null && i.Embedding != null)
            .Select(i => new { i.InformationId, i.Text, Distance = i.Embedding!.CosineDistance(v) })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => NoteHit(r.InformationId, r.Text!, 1 - r.Distance)).ToList();
    }

    private async Task<List<SearchHit>> FullTextNotesAsync(PublicPortfolioDbContext db, string tsQuery, int limit,
        CancellationToken ct)
    {
        var rows = await db.Information
            .Where(i => i.Text != null)
            .Select(i => new { i.InformationId, i.Text, Document = EF.Functions.ToTsVector(Config, i.Text!) })
            .Where(x => x.Document.Matches(EF.Functions.WebSearchToTsQuery(Config, tsQuery)))
            .Select(x => new { x.InformationId, x.Text,
                Rank = x.Document.Rank(EF.Functions.WebSearchToTsQuery(Config, tsQuery)) })
            .OrderByDescending(x => x.Rank)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(r => NoteHit(r.InformationId, r.Text!, r.Rank)).ToList();
    }

    // Notes have no page of their own, so they link to the site's home page.
    private SearchHit NoteHit(Guid id, string text, double score) =>
        new(SearchTypes.Note, id, null, SearchText.Snippet(text, SearchText.NoteTitleLength),
            SearchText.Snippet(text), Round(score), urls.Page(""));

    /// <summary>
    /// Same slugs as IPortfolioReadService: assigned over every project, active or not, by
    /// DisplayOrder then id. The integration tests check the two agree.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, string>> LoadSlugsAsync(PublicPortfolioDbContext db,
        CancellationToken ct)
    {
        var rows = await db.Projects
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.ProjectId)
            .Select(p => new { p.ProjectId, p.Title })
            .ToListAsync(ct);
        return Slugger.AssignUnique(rows.Select(r => (r.ProjectId, (string?)r.Title)));
    }

    private static double Round(double score) => Math.Round(score, 4);
}
