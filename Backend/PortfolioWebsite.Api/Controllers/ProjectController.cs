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
            .Include(p => p.WorkExperience)
            .Where(p => p.IsActive)
            .ToListAsync();

        return projects
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.IsFeatured ? p.DisplayOrder : int.MaxValue)  // featured: by their own order
            .ThenBy(p => p.WorkExperience?.DisplayOrder ?? int.MaxValue) // non-featured: employer recency
            .ThenBy(p => p.DisplayOrder)                                 // within employer: project order
            .Select(ProjectDto.FromModel);
    }

    [HttpGet("featured")]
    public async Task<IEnumerable<ProjectDto>> GetFeatured()
    {
        _logger.LogInformation("GET /projects/featured from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        var projects = await _dbContext.Projects
            .Include(p => p.WorkExperience)
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return projects.Select(ProjectDto.FromModel);
    }
}