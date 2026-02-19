using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Dtos;
using System.Text.Json;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ProjectController(ILogger<ProjectController> _logger, SqlDbContext _dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProjectDto>> GetAll()
    {
        _logger.LogInformation("GET /projects from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        return await _dbContext.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new ProjectDto
                {
                    ProjectId = p.ProjectId,
                    Title = p.Title,
                    Employer = p.Employer,
                    Role = p.Role,
                    Summary = p.Summary,
                    TechStack = Enumerable.ToList(JsonSerializer.Deserialize<List<string>>(p.TechStack, (JsonSerializerOptions)null) ?? new List<string>()),
                    DisplayOrder = p.DisplayOrder,
                    IsFeatured = p.IsFeatured,
                    StartYear = p.StartYear,
                    EndYear = p.EndYear
                })
                .ToListAsync();

    }

    [HttpGet("featured")]
    public async Task<IEnumerable<ProjectDto>> GetFeatured()
    {
        _logger.LogInformation("GET /projects/featured from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);

        var projects = await _dbContext.Projects
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return projects.Select(ProjectDto.FromModel);
    }
}