using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Dtos;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WorkExperienceController(SqlDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var jobs = await dbContext.WorkExperiences
            .Where(j => j.IsActive)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();

        return Ok(jobs.Select(WorkExperienceDto.FromModel));
    }
}
