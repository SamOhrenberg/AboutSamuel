using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/projects")]
[Authorize(Roles = "Admin")]
public class AdminProjectController(AdminProjectService _projects) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProjectDto>> GetAll(CancellationToken ct) => await _projects.ListAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminProjectRequest request, CancellationToken ct) =>
        (await _projects.CreateAsync(request, ct)).ToActionResult(Ok);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminProjectRequest request, CancellationToken ct) =>
        (await _projects.UpdateAsync(id, request, ct)).ToActionResult(Ok);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _projects.DeleteAsync(id, ct)).ToActionResult(NoContent);

    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) =>
        (await _projects.RestoreAsync(id, ct)).ToActionResult(Ok);

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ProjectReorderItem> items, CancellationToken ct)
    {
        await _projects.ReorderAsync(items, ct);
        return NoContent();
    }
}
