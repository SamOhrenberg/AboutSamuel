using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;
using static PortfolioWebsite.Api.AdminMcp.Tools.AdminToolSupport;

namespace PortfolioWebsite.Api.AdminMcp.Tools;

/// <summary>
/// The site's content: projects, work experience, and the information entries SamuelLM
/// answers from. Every write rebuilds the changed item's embedding so the chatbot's retrieval
/// matches the new text. The skill map is left alone; admin_regenerate_skill_map redraws it.
/// </summary>
[McpServerToolType]
public sealed class AdminContentTools(
    AdminProjectService projects,
    AdminWorkExperienceService jobs,
    AdminInformationService information,
    AdminEmbeddingService embeddings)
{
    private const string YearHint = "A year such as \"2023\", or \"Present\".";

    // ---- projects ----------------------------------------------------------------

    [McpServerTool(Name = "admin_list_projects", Title = "List projects (admin)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Every project, including hidden ones, in display order, with all fields, ids and whether it has an embedding.")]
    public async Task<ItemList<ProjectDto>> ListProjects(CancellationToken ct = default) =>
        new(await projects.ListAsync(ct));

    [McpServerTool(Name = "admin_get_project", Title = "Get a project (admin)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("One project by id, with every field.")]
    public async Task<ProjectDto> GetProject(
        [Description("The project's id.")] Guid projectId,
        CancellationToken ct = default) =>
        (await projects.GetAsync(projectId, ct)).Unwrap("project");

    [McpServerTool(Name = "admin_create_project", Title = "Create a project",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Adds a project to the portfolio. It's public on the site right away unless isActive is
        false. With no displayOrder it goes last.
        """)]
    public async Task<SavedItem<ProjectDto>> CreateProject(
        [Description("Project title.")] string title,
        [Description("Samuel's role on the project.")] string role,
        [Description("One or two sentence summary shown on the project card.")] string summary,
        [Description("The full write-up (Markdown).")] string? detail = null,
        [Description("One sentence on the outcome or impact.")] string? impactStatement = null,
        [Description("Technologies used, for example [\"C#\", \"PostgreSQL\"].")] List<string>? techStack = null,
        [Description(YearHint)] string? startYear = null,
        [Description(YearHint)] string? endYear = null,
        [Description("Ids of the work experience entries (employers) this project belongs to.")] List<Guid>? workExperienceIds = null,
        [Description("Show it among the featured projects.")] bool isFeatured = false,
        [Description("False keeps it hidden from the public site.")] bool isActive = true,
        [Description("Position in the list; lower comes first.")] int? displayOrder = null,
        CancellationToken ct = default)
    {
        var order = displayOrder ?? (await projects.ListAsync(ct)).Select(p => p.DisplayOrder).DefaultIfEmpty(-1).Max() + 1;
        var request = new AdminProjectRequest
        {
            Title = Required(title, nameof(title)),
            Role = Required(role, nameof(role)),
            Summary = Required(summary, nameof(summary), MediumText),
            Detail = Optional(detail, nameof(detail), LongText),
            ImpactStatement = Optional(impactStatement, nameof(impactStatement), MediumText),
            TechStack = Strings(techStack, nameof(techStack)),
            StartYear = Optional(startYear, nameof(startYear), 20),
            EndYear = Optional(endYear, nameof(endYear), 20),
            WorkExperienceIds = workExperienceIds ?? [],
            IsFeatured = isFeatured,
            IsActive = isActive,
            DisplayOrder = order,
        };
        var project = (await projects.CreateAsync(request, ct)).Unwrap("project");
        return await WithEmbedding(project, "project", project.ProjectId, project.IsActive, ct);
    }

    [McpServerTool(Name = "admin_update_project", Title = "Update a project",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Changes a project. Only the fields given change. For text fields an empty string
        clears the field; lists given replace the whole list.
        """)]
    public async Task<SavedItem<ProjectDto>> UpdateProject(
        [Description("The project's id.")] Guid projectId,
        string? title = null,
        string? role = null,
        string? summary = null,
        [Description("The full write-up (Markdown).")] string? detail = null,
        string? impactStatement = null,
        [Description("Replaces the whole tech stack.")] List<string>? techStack = null,
        [Description(YearHint)] string? startYear = null,
        [Description(YearHint)] string? endYear = null,
        [Description("Replaces the linked work experience entries.")] List<Guid>? workExperienceIds = null,
        bool? isFeatured = null,
        [Description("False hides it from the public site.")] bool? isActive = null,
        int? displayOrder = null,
        CancellationToken ct = default)
    {
        var current = (await projects.GetAsync(projectId, ct)).Unwrap("project");
        var request = new AdminProjectRequest
        {
            Title = title is null ? current.Title : Required(title, nameof(title)),
            Role = role is null ? current.Role : Required(role, nameof(role)),
            Summary = summary is null ? current.Summary : Required(summary, nameof(summary), MediumText),
            Detail = Patch(detail, current.Detail, nameof(detail), LongText),
            ImpactStatement = Patch(impactStatement, current.ImpactStatement, nameof(impactStatement), MediumText),
            TechStack = techStack is null ? current.TechStack : Strings(techStack, nameof(techStack)),
            StartYear = Patch(startYear, current.StartYear, nameof(startYear), 20),
            EndYear = Patch(endYear, current.EndYear, nameof(endYear), 20),
            WorkExperienceIds = workExperienceIds ?? current.WorkExperienceIds,
            IsFeatured = isFeatured ?? current.IsFeatured,
            IsActive = isActive ?? current.IsActive,
            DisplayOrder = displayOrder ?? current.DisplayOrder,
        };
        var project = (await projects.UpdateAsync(projectId, request, ct)).Unwrap("project");
        return await WithEmbedding(project, "project", project.ProjectId, project.IsActive, ct);
    }

    [McpServerTool(Name = "admin_hide_project", Title = "Hide a project",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Hides a project from the public site (soft delete). admin_restore_project brings it back.")]
    public async Task<Done> HideProject(
        [Description("The project's id.")] Guid projectId,
        CancellationToken ct = default)
    {
        (await projects.DeleteAsync(projectId, ct)).Unwrap("project");
        return new("Hidden.");
    }

    [McpServerTool(Name = "admin_restore_project", Title = "Restore a hidden project",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Makes a hidden project public again.")]
    public async Task<SavedItem<ProjectDto>> RestoreProject(
        [Description("The project's id.")] Guid projectId,
        CancellationToken ct = default)
    {
        var project = (await projects.RestoreAsync(projectId, ct)).Unwrap("project");
        return await WithEmbedding(project, "project", project.ProjectId, true, ct);
    }

    [McpServerTool(Name = "admin_reorder_projects", Title = "Reorder projects",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Sets the display order: each listed project gets its position in the list (0 first).
        Projects not listed keep their current order value.
        """)]
    public async Task<Done> ReorderProjects(
        [Description("Project ids, in the order they should appear.")] List<Guid> projectIds,
        CancellationToken ct = default)
    {
        if (projectIds.Count is 0 or > MaxListItems)
            throw new McpException($"projectIds needs between 1 and {MaxListItems} ids.");
        await projects.ReorderAsync(projectIds.Select((id, i) => new ProjectReorderItem(id, i)).ToList(), ct);
        return new("Reordered.");
    }

    // ---- work experience ---------------------------------------------------------

    [McpServerTool(Name = "admin_list_work_experience", Title = "List work experience (admin)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Every work experience entry (employer and role), including hidden ones, in display order.")]
    public async Task<ItemList<WorkExperienceDto>> ListWorkExperience(CancellationToken ct = default) =>
        new(await jobs.ListAsync(ct));

    [McpServerTool(Name = "admin_get_work_experience", Title = "Get a work experience entry",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("One work experience entry by id.")]
    public async Task<WorkExperienceDto> GetWorkExperience(
        [Description("The entry's id.")] Guid workExperienceId,
        CancellationToken ct = default) =>
        (await jobs.GetAsync(workExperienceId, ct)).Unwrap("work experience entry");

    [McpServerTool(Name = "admin_create_work_experience", Title = "Create a work experience entry",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Adds an employer and role. With no displayOrder it goes last.")]
    public async Task<SavedItem<WorkExperienceDto>> CreateWorkExperience(
        [Description("Company name.")] string employer,
        [Description("Job title.")] string title,
        [Description(YearHint)] string? startYear = null,
        [Description(YearHint)] string? endYear = null,
        [Description("A short paragraph about the role.")] string? summary = null,
        [Description("Achievement bullets.")] List<string>? achievements = null,
        [Description("False keeps it hidden from the public site.")] bool isActive = true,
        int? displayOrder = null,
        CancellationToken ct = default)
    {
        var order = displayOrder ?? (await jobs.ListAsync(ct)).Select(j => j.DisplayOrder).DefaultIfEmpty(-1).Max() + 1;
        var job = await jobs.CreateAsync(new AdminWorkExperienceRequest
        {
            Employer = Required(employer, nameof(employer)),
            Title = Required(title, nameof(title)),
            StartYear = Optional(startYear, nameof(startYear), 20),
            EndYear = Optional(endYear, nameof(endYear), 20),
            Summary = Optional(summary, nameof(summary), MediumText),
            Achievements = Strings(achievements, nameof(achievements), MediumText),
            IsActive = isActive,
            DisplayOrder = order,
        }, ct);
        return await WithEmbedding(job, "work-experience", job.WorkExperienceId, job.IsActive, ct);
    }

    [McpServerTool(Name = "admin_update_work_experience", Title = "Update a work experience entry",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Changes a work experience entry. Only the fields given change. For text fields an empty
        string clears the field; achievements given replace the whole list.
        """)]
    public async Task<SavedItem<WorkExperienceDto>> UpdateWorkExperience(
        [Description("The entry's id.")] Guid workExperienceId,
        string? employer = null,
        string? title = null,
        [Description(YearHint)] string? startYear = null,
        [Description(YearHint)] string? endYear = null,
        string? summary = null,
        [Description("Replaces every achievement bullet.")] List<string>? achievements = null,
        [Description("False hides it from the public site.")] bool? isActive = null,
        int? displayOrder = null,
        CancellationToken ct = default)
    {
        var current = (await jobs.GetAsync(workExperienceId, ct)).Unwrap("work experience entry");
        var job = (await jobs.UpdateAsync(workExperienceId, new AdminWorkExperienceRequest
        {
            Employer = employer is null ? current.Employer : Required(employer, nameof(employer)),
            Title = title is null ? current.Title : Required(title, nameof(title)),
            StartYear = Patch(startYear, current.StartYear, nameof(startYear), 20),
            EndYear = Patch(endYear, current.EndYear, nameof(endYear), 20),
            Summary = Patch(summary, current.Summary, nameof(summary), MediumText),
            Achievements = achievements is null ? current.Achievements : Strings(achievements, nameof(achievements), MediumText),
            IsActive = isActive ?? current.IsActive,
            DisplayOrder = displayOrder ?? current.DisplayOrder,
        }, ct)).Unwrap("work experience entry");
        return await WithEmbedding(job, "work-experience", job.WorkExperienceId, job.IsActive, ct);
    }

    [McpServerTool(Name = "admin_delete_work_experience", Title = "Delete a work experience entry",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Permanently deletes a work experience entry and unlinks it from its projects. To only
        hide it, use admin_update_work_experience with isActive false.
        """)]
    public async Task<Done> DeleteWorkExperience(
        [Description("The entry's id.")] Guid workExperienceId,
        CancellationToken ct = default)
    {
        (await jobs.DeleteAsync(workExperienceId, ct)).Unwrap("work experience entry");
        return new("Deleted.");
    }

    [McpServerTool(Name = "admin_reorder_work_experience", Title = "Reorder work experience",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Sets the display order: each listed entry gets its position in the list (0 first).")]
    public async Task<Done> ReorderWorkExperience(
        [Description("Work experience ids, in the order they should appear.")] List<Guid> workExperienceIds,
        CancellationToken ct = default)
    {
        if (workExperienceIds.Count is 0 or > MaxListItems)
            throw new McpException($"workExperienceIds needs between 1 and {MaxListItems} ids.");
        await jobs.ReorderAsync(workExperienceIds, ct);
        return new("Reordered.");
    }

    // ---- information ---------------------------------------------------------------

    [McpServerTool(Name = "admin_list_information", Title = "List information entries",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Every information entry: the short facts about Samuel that SamuelLM retrieves to answer
        visitors, each with its keywords and whether it has an embedding.
        """)]
    public async Task<ItemList<AdminInformationDto>> ListInformation(CancellationToken ct = default) =>
        new(await information.ListAsync(ct));

    [McpServerTool(Name = "admin_get_information", Title = "Get an information entry",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("One information entry by id.")]
    public async Task<AdminInformationDto> GetInformation(
        [Description("The entry's id.")] Guid informationId,
        CancellationToken ct = default) =>
        (await information.GetAsync(informationId, ct)).Unwrap("information entry");

    [McpServerTool(Name = "admin_create_information", Title = "Create an information entry",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Adds a fact SamuelLM can answer from.")]
    public async Task<SavedItem<AdminInformationDto>> CreateInformation(
        [Description("The fact, written as Samuel would say it.")] string text,
        [Description("Keywords that help retrieval find it.")] List<string>? keywords = null,
        CancellationToken ct = default)
    {
        var item = await information.CreateAsync(new AdminInformationRequest
        {
            Text = Required(text, nameof(text), MediumText),
            Keywords = Strings(keywords, nameof(keywords)),
        }, ct);
        return await WithEmbedding(item, "information", item.InformationId, true, ct);
    }

    [McpServerTool(Name = "admin_update_information", Title = "Update an information entry",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Changes an information entry. Only the fields given change; keywords given replace the whole list.")]
    public async Task<SavedItem<AdminInformationDto>> UpdateInformation(
        [Description("The entry's id.")] Guid informationId,
        string? text = null,
        [Description("Replaces every keyword.")] List<string>? keywords = null,
        CancellationToken ct = default)
    {
        var current = (await information.GetAsync(informationId, ct)).Unwrap("information entry");
        var item = (await information.UpdateAsync(informationId, new AdminInformationRequest
        {
            Text = text is null ? current.Text : Required(text, nameof(text), MediumText),
            Keywords = keywords is null ? current.Keywords : Strings(keywords, nameof(keywords)),
        }, ct)).Unwrap("information entry");
        return await WithEmbedding(item, "information", item.InformationId, true, ct);
    }

    [McpServerTool(Name = "admin_delete_information", Title = "Delete an information entry",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Permanently deletes an information entry and its keywords.")]
    public async Task<Done> DeleteInformation(
        [Description("The entry's id.")] Guid informationId,
        CancellationToken ct = default)
    {
        (await information.DeleteAsync(informationId, ct)).Unwrap("information entry");
        return new("Deleted.");
    }

    /// <summary>
    /// Rebuilds the item's embedding after a write. Hidden projects and jobs aren't embedded
    /// (the bulk job skips them too). A failed embedding doesn't undo the write; it's reported
    /// and admin_generate_missing_embeddings can retry it.
    /// </summary>
    private async Task<SavedItem<T>> WithEmbedding<T>(T item, string type, Guid id, bool active, CancellationToken ct)
    {
        if (!active) return new(item, false);
        var result = await embeddings.GenerateForItemAsync(type, id, ct);
        if (!result.IsOk) return new(item, false);

        // Read it back so hasEmbedding in the result is true
        object reloaded = type switch
        {
            "project" => (await projects.GetAsync(id, ct)).Unwrap("project"),
            "work-experience" => (await jobs.GetAsync(id, ct)).Unwrap("work experience entry"),
            _ => (await information.GetAsync(id, ct)).Unwrap("information entry"),
        };
        return new((T)reloaded, true);
    }
}
