using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Dtos;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ProjectController(ILogger<ProjectController> _logger, SqlDbContext _dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProjectDto>> GetAll()
    {
        _logger.LogInformation("GET /projects from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        var projects = await _dbContext.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.IsActive)
            .ToListAsync();

        return projects
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.IsFeatured ? p.DisplayOrder : int.MaxValue)
            .ThenBy(p => p.WorkExperiences.Any()
                ? p.WorkExperiences.Min(w => w.DisplayOrder)
                : int.MaxValue)
            .ThenBy(p => p.DisplayOrder)
            .Select(ProjectDto.FromModel);
    }

    [HttpGet("featured")]
    public async Task<IEnumerable<ProjectDto>> GetFeatured()
    {
        _logger.LogInformation("GET /projects/featured from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        var projects = await _dbContext.Projects
            .Include(p => p.WorkExperiences)
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return projects.Select(ProjectDto.FromModel);
    }
}