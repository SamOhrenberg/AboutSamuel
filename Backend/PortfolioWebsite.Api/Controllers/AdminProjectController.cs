using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;
using System.Text.Json;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/projects")]
[Authorize(Roles = "Admin")]
public class AdminProjectController(
    ILogger<AdminProjectController> _logger,
    SqlDbContext _db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProjectDto>> GetAll()
    {
        _logger.LogInformation("Admin GET /admin/projects");

        var projects = await _db.Projects
            .Include(p => p.WorkExperience)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return projects.Select(ProjectDto.FromModel);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminProjectRequest request)
    {
        if (request.WorkExperienceId.HasValue &&
            !await _db.WorkExperiences.AnyAsync(w => w.WorkExperienceId == request.WorkExperienceId))
            return BadRequest(new { Message = "WorkExperienceId does not reference a known work experience." });

        var project = MapToModel(request, new Project { ProjectId = Guid.NewGuid() });

        await _db.Projects.AddAsync(project);
        await _db.SaveChangesAsync();

        // Re-fetch with navigation so the DTO has Employer populated
        await _db.Entry(project).Reference(p => p.WorkExperience).LoadAsync();

        _logger.LogInformation("Admin created project {ProjectId}: {Title}", project.ProjectId, project.Title);
        return Ok(ProjectDto.FromModel(project));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminProjectRequest request)
    {
        if (request.WorkExperienceId.HasValue &&
            !await _db.WorkExperiences.AnyAsync(w => w.WorkExperienceId == request.WorkExperienceId))
            return BadRequest(new { Message = "WorkExperienceId does not reference a known work experience." });

        var project = await _db.Projects
            .Include(p => p.WorkExperience)
            .FirstOrDefaultAsync(p => p.ProjectId == id);

        if (project is null) return NotFound();

        MapToModel(request, project);
        await _db.SaveChangesAsync();

        // Navigation may have changed — reload if FK changed
        if (project.WorkExperienceId != request.WorkExperienceId)
            await _db.Entry(project).Reference(p => p.WorkExperience).LoadAsync();

        _logger.LogInformation("Admin updated project {ProjectId}", id);
        return Ok(ProjectDto.FromModel(project));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        project.IsActive = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin soft-deleted project {ProjectId}", id);
        return NoContent();
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        project.IsActive = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin restored project {ProjectId}", id);
        return Ok(ProjectDto.FromModel(project));
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ProjectReorderItem> items)
    {
        var ids = items.Select(i => i.ProjectId).ToList();
        var projects = await _db.Projects
            .Where(p => ids.Contains(p.ProjectId))
            .ToListAsync();

        foreach (var item in items)
        {
            var project = projects.FirstOrDefault(p => p.ProjectId == item.ProjectId);
            if (project is not null) project.DisplayOrder = item.DisplayOrder;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static Project MapToModel(AdminProjectRequest request, Project project)
    {
        project.WorkExperienceId = request.WorkExperienceId;
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
        project.EmbeddingJson = null;
        return project;
    }
}

public class ProjectReorderItem
{
    public Guid ProjectId { get; set; }
    public int DisplayOrder { get; set; }
}