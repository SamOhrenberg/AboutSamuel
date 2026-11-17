using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("/admin/Work-experience")]
public class AdminWorkExperienceController(AdminWorkExperienceService _jobs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _jobs.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        (await _jobs.GetAsync(id, ct)).ToActionResult(Ok);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminWorkExperienceRequest request, CancellationToken ct)
    {
        var job = await _jobs.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = job.WorkExperienceId }, job);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminWorkExperienceRequest request, CancellationToken ct) =>
        (await _jobs.UpdateAsync(id, request, ct)).ToActionResult(Ok);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _jobs.DeleteAsync(id, ct)).ToActionResult(NoContent);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<Guid> orderedIds, CancellationToken ct)
    {
        await _jobs.ReorderAsync(orderedIds, ct);
        return NoContent();
    }
}
