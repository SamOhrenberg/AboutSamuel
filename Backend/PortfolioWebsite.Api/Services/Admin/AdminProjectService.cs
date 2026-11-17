using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;

namespace PortfolioWebsite.Api.Services.Admin;

public record ProjectReorderItem(Guid ProjectId, int DisplayOrder);

/// <summary>Projects as the admin panel and the admin MCP tools edit them.</summary>
public class AdminProjectService(SqlDbContext _db, ILogger<AdminProjectService> _logger)
{
    private const string UnknownWorkExperience = "One or more WorkExperienceIds do not reference known work experiences.";

    public async Task<List<ProjectDto>> ListAsync(CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Include(p => p.WorkExperiences)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
        return projects.Select(ProjectDto.FromModel).ToList();
    }

    public async Task<AdminResult<ProjectDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.WorkExperiences)
            .FirstOrDefaultAsync(p => p.ProjectId == id, ct);
        return project is null ? AdminFailure.NotFound() : ProjectDto.FromModel(project);
    }

    public async Task<AdminResult<ProjectDto>> CreateAsync(AdminProjectRequest request, CancellationToken ct = default)
    {
        var workExperiences = await FindWorkExperiencesAsync(request.WorkExperienceIds, ct);
        if (workExperiences is null) return AdminFailure.Invalid(UnknownWorkExperience);

        var project = MapToModel(request, new Project { ProjectId = Guid.NewGuid() });
        project.WorkExperiences = workExperiences;

        await _db.Projects.AddAsync(project, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin created project {ProjectId}: {Title}", project.ProjectId, project.Title);
        return ProjectDto.FromModel(project);
    }

    public async Task<AdminResult<ProjectDto>> UpdateAsync(Guid id, AdminProjectRequest request, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.WorkExperiences)
            .FirstOrDefaultAsync(p => p.ProjectId == id, ct);
        if (project is null) return AdminFailure.NotFound();

        var workExperiences = await FindWorkExperiencesAsync(request.WorkExperienceIds, ct);
        if (workExperiences is null) return AdminFailure.Invalid(UnknownWorkExperience);

        MapToModel(request, project);

        // Replace the collection; EF tracks the join table changes
        project.WorkExperiences.Clear();
        foreach (var we in workExperiences)
            project.WorkExperiences.Add(we);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin updated project {ProjectId}", id);
        return ProjectDto.FromModel(project);
    }

    /// <summary>Soft delete: the project is hidden, not removed.</summary>
    public async Task<AdminResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FindAsync([id], ct);
        if (project is null) return AdminFailure.NotFound();

        project.IsActive = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin soft-deleted project {ProjectId}", id);
        return AdminResult.Ok();
    }

    public async Task<AdminResult<ProjectDto>> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.WorkExperiences)
            .FirstOrDefaultAsync(p => p.ProjectId == id, ct);
        if (project is null) return AdminFailure.NotFound();

        project.IsActive = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin restored project {ProjectId}", id);
        return ProjectDto.FromModel(project);
    }

    /// <summary>Sets DisplayOrder on the listed projects. Unknown ids are skipped.</summary>
    public async Task ReorderAsync(IReadOnlyList<ProjectReorderItem> items, CancellationToken ct = default)
    {
        var ids = items.Select(i => i.ProjectId).ToList();
        var projects = await _db.Projects.Where(p => ids.Contains(p.ProjectId)).ToListAsync(ct);

        foreach (var item in items)
        {
            var project = projects.FirstOrDefault(p => p.ProjectId == item.ProjectId);
            if (project is not null) project.DisplayOrder = item.DisplayOrder;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>The work experiences with these ids, or null when any of them doesn't exist.</summary>
    private async Task<List<WorkExperience>?> FindWorkExperiencesAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var distinct = ids.Distinct().ToList();
        var found = await _db.WorkExperiences.Where(w => distinct.Contains(w.WorkExperienceId)).ToListAsync(ct);
        return found.Count == distinct.Count ? found : null;
    }

    private static Project MapToModel(AdminProjectRequest request, Project project)
    {
        project.Title = request.Title;
        project.Role = request.Role;
        project.Summary = request.Summary;
        project.Detail = request.Detail;
        project.ImpactStatement = request.ImpactStatement;
        project.TechStack = JsonSerializer.Serialize(request.TechStack);
        project.DisplayOrder = request.DisplayOrder;
        project.IsFeatured = request.IsFeatured;
        project.IsActive = request.IsActive;
        project.StartYear = request.StartYear;
        project.EndYear = request.EndYear;
        return project;
    }
}
