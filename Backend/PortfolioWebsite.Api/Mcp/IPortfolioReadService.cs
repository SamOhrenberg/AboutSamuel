using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Everything the public MCP server may read. Kept deliberately narrow and read-only; a
/// future admin MCP server gets its own service, not more methods here.
/// Only active items are ever returned.
/// </summary>
public interface IPortfolioReadService
{
    public const int MaxPageSize = 25;

    Task<PagedResult<ProjectSummary>> ListProjectsAsync(ProjectQuery query, CancellationToken cancellationToken = default);

    /// <summary>Looks a project up by slug or by GUID. Null if missing or inactive.</summary>
    Task<ProjectDetail?> GetProjectAsync(string slugOrId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillSummary>> GetSkillsAsync(int minProjects = 1, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExperienceItem>> GetExperienceAsync(CancellationToken cancellationToken = default);
}
