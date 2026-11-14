namespace PortfolioWebsite.Api.Mcp.Contracts;

// Shapes returned to MCP clients. Separate from the site's DTOs on purpose: no IsActive,
// DisplayOrder or embedding flags, and every item carries a url back to the site.
// Years are the strings as entered; a null endYear means the item is ongoing (isCurrent).

public sealed record ProjectRef(
    string Slug,
    Guid Id,
    string Title,
    string? StartYear,
    string? EndYear,
    string Url);

public sealed record ExperienceRef(
    Guid Id,
    string Employer,
    string Title,
    string Url);

public sealed record ProjectSummary(
    string Slug,
    Guid Id,
    string Title,
    string Role,
    string Summary,
    IReadOnlyList<string> TechStack,
    string? StartYear,
    string? EndYear,
    bool IsCurrent,
    bool Featured,
    IReadOnlyList<ExperienceRef> Experiences,
    string Url);

public sealed record ProjectDetail(
    string Slug,
    Guid Id,
    string Title,
    string Role,
    string Summary,
    string? Detail,
    string? ImpactStatement,
    IReadOnlyList<string> TechStack,
    string? StartYear,
    string? EndYear,
    bool IsCurrent,
    bool Featured,
    IReadOnlyList<ExperienceRef> Experiences,
    string Url);

public sealed record ExperienceItem(
    Guid Id,
    string Employer,
    string Title,
    string? StartYear,
    string? EndYear,
    bool IsCurrent,
    string? Summary,
    IReadOnlyList<string> Achievements,
    IReadOnlyList<ProjectRef> Projects,
    string Url);

public sealed record SkillSummary(
    string Name,
    int ProjectCount,
    int? FirstYear,
    int? LastYear,
    bool IsCurrent,
    IReadOnlyList<ProjectRef> Projects,
    IReadOnlyList<ExperienceRef> Employers);

public sealed record ProjectQuery(
    string? Tech = null,
    int? Year = null,
    bool FeaturedOnly = false,
    int Page = 1,
    int PageSize = 10);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore);
