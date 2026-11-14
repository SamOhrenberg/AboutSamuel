using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// The public, read-only portfolio tools. Thin wrappers: validate input, call the read
/// service, shape the result. Every tool is annotated read-only, non-destructive,
/// idempotent and closed-world, and returns structured content with an output schema.
/// </summary>
[McpServerToolType]
public sealed class PortfolioTools(
    IPortfolioReadService portfolio,
    IOptions<McpProfileOptions> profile,
    PortfolioUrls urls)
{
    [McpServerTool(Name = "get_profile", Title = "Get Samuel's profile",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Samuel Ohrenberg's short self-description: name, headline, summary, current focus,
        availability and links, written by Samuel himself. A null availability means he
        hasn't said, not that he is unavailable.
        """)]
    public ProfileResult GetProfile() => profile.Value.ToResult(urls);

    [McpServerTool(Name = "list_projects", Title = "List projects",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Lists Samuel's portfolio projects in the same order as the site (featured first),
        with role, summary, tech stack, years, linked employers and a url. Optional filters
        combine with AND. Results are paged (pageSize at most 25) and hasMore says whether
        another page exists. Summaries only, without the full write-up.
        """)]
    public async Task<PagedResult<ProjectSummary>> ListProjects(
        [Description("Only projects whose tech stack includes this exact technology, case-insensitive (for example \"C#\" or \"PostgreSQL\").")]
        [MaxLength(McpInput.MaxTextLength)]
        string? tech = null,
        [Description("Only projects active during this calendar year, for example 2023.")]
        [Range(McpInput.MinYear, McpInput.MaxYear)]
        int? year = null,
        [Description("Only projects Samuel has marked as featured.")]
        bool featuredOnly = false,
        [Description("1-based page number.")]
        [Range(1, McpInput.MaxPage)]
        int page = 1,
        [Description("Items per page, 1 to 25.")]
        [Range(1, IPortfolioReadService.MaxPageSize)]
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new ProjectQuery(
            McpInput.OptionalText(tech, nameof(tech)),
            McpInput.OptionalRange(year, nameof(year), McpInput.MinYear, McpInput.MaxYear),
            featuredOnly,
            McpInput.Range(page, nameof(page), 1, McpInput.MaxPage),
            McpInput.Range(pageSize, nameof(pageSize), 1, IPortfolioReadService.MaxPageSize));

        return await portfolio.ListProjectsAsync(query, cancellationToken);
    }

    [McpServerTool(Name = "get_project", Title = "Get a project",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        The full write-up of one project: role, summary, tech stack, years, linked employers
        and url, plus the detailed description and impact statement. Looked up by slug (for
        example "portfolio-chat") or GUID id, as they appear in other results.
        """)]
    public async Task<ProjectDetail> GetProject(
        [Description("The project's slug or its GUID id.")]
        [MaxLength(McpInput.MaxSlugOrIdLength)]
        string slugOrId,
        CancellationToken cancellationToken = default)
    {
        var key = McpInput.RequiredText(slugOrId, nameof(slugOrId), McpInput.MaxSlugOrIdLength);

        return await portfolio.GetProjectAsync(key, cancellationToken)
            ?? throw new McpException($"No project matches '{key}'. Call list_projects for valid slugs.");
    }

    [McpServerTool(Name = "get_skills", Title = "Get skills",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Technologies Samuel has used, derived from his projects' tech stacks (there is no
        self-rated skill list). Each skill lists the projects and employers it appeared
        with, the project count, the first and last year seen, and whether it's in current
        use. Most-used first. minProjects drops one-off mentions.
        """)]
    public async Task<SkillsResult> GetSkills(
        [Description("Only skills used in at least this many projects, 1 to 100.")]
        [Range(1, McpInput.MaxMinProjects)]
        int minProjects = 1,
        CancellationToken cancellationToken = default)
    {
        var min = McpInput.Range(minProjects, nameof(minProjects), 1, McpInput.MaxMinProjects);
        var skills = await portfolio.GetSkillsAsync(min, cancellationToken);
        return new SkillsResult(skills, skills.Count);
    }

    [McpServerTool(Name = "get_experience", Title = "Get work history",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Samuel's work history in the same order as the site: employer, title,
        years, summary, achievements, and the projects done there (with their slugs).
        isCurrent marks the role he holds now.
        """)]
    public async Task<ExperienceResult> GetExperience(CancellationToken cancellationToken = default)
    {
        var experience = await portfolio.GetExperienceAsync(cancellationToken);
        return new ExperienceResult(experience, experience.Count);
    }
}
