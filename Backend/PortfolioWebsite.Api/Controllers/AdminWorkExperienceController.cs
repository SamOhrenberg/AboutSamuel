using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;
using System.Text.Json;

namespace PortfolioWebsite.Api.Controllers.Admin;

[ApiController]
[Authorize]
[Route("/admin/Work-experience")]
public class AdminWorkExperienceController(SqlDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var Works = await dbContext.WorkExperiences
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();

        return Ok(Works.Select(WorkExperienceDto.FromModel));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var Work = await dbContext.WorkExperiences.FindAsync(id);
        return Work is null ? NotFound() : Ok(WorkExperienceDto.FromModel(Work));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminWorkExperienceRequest request)
    {
        var Work = MapToModel(request, new WorkExperience
        {
            WorkExperienceId = Guid.NewGuid()
        });

        await dbContext.WorkExperiences.AddAsync(Work);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = Work.WorkExperienceId }, WorkExperienceDto.FromModel(Work));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminWorkExperienceRequest request)
    {
        var Work = await dbContext.WorkExperiences.FindAsync(id);
        if (Work is null) return NotFound();

        MapToModel(request, Work);
        await dbContext.SaveChangesAsync();

        return Ok(WorkExperienceDto.FromModel(Work));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var Work = await dbContext.WorkExperiences.FindAsync(id);
        if (Work is null) return NotFound();

        dbContext.WorkExperiences.Remove(Work);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<Guid> orderedIds)
    {
        var Works = await dbContext.WorkExperiences
            .Where(j => orderedIds.Contains(j.WorkExperienceId))
            .ToListAsync();

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var Work = Works.FirstOrDefault(j => j.WorkExperienceId == orderedIds[i]);
            if (Work is not null) Work.DisplayOrder = i;
        }

        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    private static WorkExperience MapToModel(AdminWorkExperienceRequest request, WorkExperience Work)
    {
        Work.Employer     = request.Employer;
        Work.Title        = request.Title;
        Work.StartYear    = request.StartYear;
        Work.EndYear      = request.EndYear;
        Work.Summary      = request.Summary;
        Work.Achievements = JsonSerializer.Serialize(request.Achievements);
        Work.DisplayOrder = request.DisplayOrder;
        Work.IsActive     = request.IsActive;
        Work.EmbeddingJson = null;
        return Work;
    }
}
