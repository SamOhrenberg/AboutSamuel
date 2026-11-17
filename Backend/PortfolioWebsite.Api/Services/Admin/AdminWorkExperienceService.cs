using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;

namespace PortfolioWebsite.Api.Services.Admin;

/// <summary>Work experience as the admin panel and the admin MCP tools edit it.</summary>
public class AdminWorkExperienceService(SqlDbContext _db, ILogger<AdminWorkExperienceService> _logger)
{
    public async Task<List<WorkExperienceDto>> ListAsync(CancellationToken ct = default)
    {
        var jobs = await _db.WorkExperiences.OrderBy(j => j.DisplayOrder).ToListAsync(ct);
        return jobs.Select(WorkExperienceDto.FromModel).ToList();
    }

    public async Task<AdminResult<WorkExperienceDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.WorkExperiences.FindAsync([id], ct);
        return job is null ? AdminFailure.NotFound() : WorkExperienceDto.FromModel(job);
    }

    public async Task<WorkExperienceDto> CreateAsync(AdminWorkExperienceRequest request, CancellationToken ct = default)
    {
        var job = MapToModel(request, new WorkExperience { WorkExperienceId = Guid.NewGuid() });

        await _db.WorkExperiences.AddAsync(job, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin created work experience {WorkExperienceId}", job.WorkExperienceId);
        return WorkExperienceDto.FromModel(job);
    }

    /// <summary>Replaces every field and clears the embedding, which no longer matches the text.</summary>
    public async Task<AdminResult<WorkExperienceDto>> UpdateAsync(Guid id, AdminWorkExperienceRequest request,
        CancellationToken ct = default)
    {
        var job = await _db.WorkExperiences.FindAsync([id], ct);
        if (job is null) return AdminFailure.NotFound();

        MapToModel(request, job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin updated work experience {WorkExperienceId}", id);
        return WorkExperienceDto.FromModel(job);
    }

    /// <summary>Hard delete. Links to projects go with it (cascade on the join table).</summary>
    public async Task<AdminResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.WorkExperiences.FindAsync([id], ct);
        if (job is null) return AdminFailure.NotFound();

        _db.WorkExperiences.Remove(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin deleted work experience {WorkExperienceId}", id);
        return AdminResult.Ok();
    }

    /// <summary>DisplayOrder becomes each id's position in the list. Unknown ids are skipped.</summary>
    public async Task ReorderAsync(IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var jobs = await _db.WorkExperiences.Where(j => orderedIds.Contains(j.WorkExperienceId)).ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var job = jobs.FirstOrDefault(j => j.WorkExperienceId == orderedIds[i]);
            if (job is not null) job.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static WorkExperience MapToModel(AdminWorkExperienceRequest request, WorkExperience job)
    {
        job.Employer = request.Employer;
        job.Title = request.Title;
        job.StartYear = request.StartYear;
        job.EndYear = request.EndYear;
        job.Summary = request.Summary;
        job.Achievements = JsonSerializer.Serialize(request.Achievements);
        job.DisplayOrder = request.DisplayOrder;
        job.IsActive = request.IsActive;
        job.Embedding = null;
        return job;
    }
}
