using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

public sealed class PortfolioReadService(
    IDbContextFactory<PublicPortfolioDbContext> dbFactory,
    PortfolioUrls urls,
    TimeProvider time,
    ILogger<PortfolioReadService> logger) : IPortfolioReadService
{
    public async Task<PagedResult<ProjectSummary>> ListProjectsAsync(ProjectQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken);
        var tech = query.Tech?.Trim();

        var matches = snapshot.Projects
            .Where(p => !query.FeaturedOnly || p.Featured)
            .Where(p => string.IsNullOrEmpty(tech) || p.TechStack.Contains(tech, StringComparer.OrdinalIgnoreCase))
            .Where(p => query.Year is not { } year || p.Years.Contains(year))
            .ToList();

        var pageSize = Math.Clamp(query.PageSize, 1, IPortfolioReadService.MaxPageSize);
        var page = Math.Max(1, query.Page);
        var items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.ToSummary())
            .ToList();

        return new PagedResult<ProjectSummary>(items, page, pageSize, matches.Count,
            HasMore: page * pageSize < matches.Count);
    }

    public async Task<ProjectDetail?> GetProjectAsync(string slugOrId, CancellationToken cancellationToken = default)
    {
        var key = slugOrId?.Trim() ?? "";
        if (key.Length == 0)
            return null;

        var snapshot = await LoadAsync(cancellationToken);
        var project = Guid.TryParse(key, out var id)
            ? snapshot.Projects.FirstOrDefault(p => p.Id == id)
            : snapshot.Projects.FirstOrDefault(p => p.Slug.Equals(key, StringComparison.OrdinalIgnoreCase));

        return project?.ToDetail();
    }

    public async Task<IReadOnlyList<SkillSummary>> GetSkillsAsync(int minProjects = 1, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken);
        return SkillAggregator.Aggregate(
            snapshot.Projects.Select(p => new SkillSource(p.Ref, p.TechStack, p.Years, p.Experiences)),
            minProjects);
    }

    public async Task<IReadOnlyList<ExperienceItem>> GetExperienceAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken);
        return snapshot.Experiences
            .Select(e => new ExperienceItem(
                e.Ref.Id, e.Ref.Employer, e.Ref.Title, e.StartYear, e.EndYear, e.Years.IsCurrent,
                e.Summary, e.Achievements,
                snapshot.Projects.Where(p => p.Experiences.Any(x => x.Id == e.Ref.Id)).Select(p => p.Ref).ToList(),
                e.Ref.Url))
            .ToList();
    }

    /// <summary>
    /// Reads all projects and active experiences (both tables are small), then shapes them.
    /// Slugs are assigned across every project, active or not, in display order, so hiding
    /// one never shifts another's suffix. Only active projects make it into the snapshot.
    /// </summary>
    private async Task<Snapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Explicit projections keep the embedding columns out of these queries.
        var projectRows = await db.Projects
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.ProjectId)
            .Select(p => new
            {
                p.ProjectId, p.Title, p.Role, p.Summary, p.Detail, p.ImpactStatement, p.TechStack,
                p.DisplayOrder, p.IsFeatured, p.IsActive, p.StartYear, p.EndYear,
                ExperienceIds = p.WorkExperiences.Select(w => w.WorkExperienceId).ToList(),
            })
            .ToListAsync(cancellationToken);

        var experienceRows = await db.WorkExperiences
            .Where(w => w.IsActive)
            .OrderBy(w => w.DisplayOrder).ThenBy(w => w.WorkExperienceId)
            .Select(w => new
            {
                w.WorkExperienceId, w.Employer, w.Title, w.StartYear, w.EndYear, w.Summary,
                w.Achievements, w.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        var currentYear = time.GetUtcNow().Year;

        var experiences = experienceRows.Select(w => new ExperienceView(
                new ExperienceRef(w.WorkExperienceId, w.Employer, w.Title, urls.Experience(w.WorkExperienceId)),
                w.StartYear, w.EndYear, YearRange.From(w.StartYear, w.EndYear, currentYear),
                w.Summary, ParseArray(w.Achievements, "WorkExperience", w.WorkExperienceId, "Achievements"),
                w.DisplayOrder))
            .ToList();
        var experienceById = experiences.ToDictionary(e => e.Ref.Id);

        var slugs = Slugger.AssignUnique(projectRows.Select(p => (p.ProjectId, (string?)p.Title)));

        var projects = projectRows
            .Where(p => p.IsActive)
            .Select(p =>
            {
                var linked = p.ExperienceIds
                    .Where(experienceById.ContainsKey)
                    .Select(id => experienceById[id])
                    .OrderBy(e => e.DisplayOrder)
                    .ToList();

                return new ProjectView(
                    new ProjectRef(slugs[p.ProjectId], p.ProjectId, p.Title, p.StartYear, p.EndYear,
                        urls.Project(p.ProjectId)),
                    p.Role, p.Summary, p.Detail, p.ImpactStatement,
                    ParseArray(p.TechStack, "Project", p.ProjectId, "TechStack"),
                    YearRange.From(p.StartYear, p.EndYear, currentYear),
                    p.IsFeatured, p.DisplayOrder,
                    linked.Select(e => e.Ref).ToList(),
                    linked.Count > 0 ? linked[0].DisplayOrder : int.MaxValue);
            })
            // Same order as the site's /projects page: featured first, then by employer, then own order.
            .OrderByDescending(p => p.Featured)
            .ThenBy(p => p.Featured ? p.DisplayOrder : int.MaxValue)
            .ThenBy(p => p.FirstExperienceOrder)
            .ThenBy(p => p.DisplayOrder)
            .ToList();

        return new Snapshot(projects, experiences);
    }

    private IReadOnlyList<string> ParseArray(string? json, string entity, Guid id, string column)
    {
        if (!JsonStringArray.TryParse(json, out var values))
            logger.LogWarning("{Entity} {Id} has malformed JSON in {Column}; treating it as empty",
                entity, id, column);
        return values;
    }

    private sealed record Snapshot(List<ProjectView> Projects, List<ExperienceView> Experiences);

    private sealed record ExperienceView(
        ExperienceRef Ref,
        string? StartYear,
        string? EndYear,
        YearRange Years,
        string? Summary,
        IReadOnlyList<string> Achievements,
        int DisplayOrder);

    private sealed record ProjectView(
        ProjectRef Ref,
        string Role,
        string Summary,
        string? Detail,
        string? ImpactStatement,
        IReadOnlyList<string> TechStack,
        YearRange Years,
        bool Featured,
        int DisplayOrder,
        IReadOnlyList<ExperienceRef> Experiences,
        int FirstExperienceOrder)
    {
        public Guid Id => Ref.Id;
        public string Slug => Ref.Slug;

        public ProjectSummary ToSummary() => new(
            Ref.Slug, Ref.Id, Ref.Title, Role, Summary, TechStack, Ref.StartYear, Ref.EndYear,
            Years.IsCurrent, Featured, Experiences, Ref.Url);

        public ProjectDetail ToDetail() => new(
            Ref.Slug, Ref.Id, Ref.Title, Role, Summary, Detail, ImpactStatement, TechStack,
            Ref.StartYear, Ref.EndYear, Years.IsCurrent, Featured, Experiences, Ref.Url);
    }
}
