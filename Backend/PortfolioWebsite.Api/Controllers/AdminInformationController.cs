using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/information")]
[Authorize(Roles = "Admin")]
public class AdminInformationController(AdminInformationService _information) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<AdminInformationDto>> GetAll(CancellationToken ct) => await _information.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        (await _information.GetAsync(id, ct)).ToActionResult(Ok);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminInformationRequest request, CancellationToken ct) =>
        Ok(await _information.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminInformationRequest request, CancellationToken ct) =>
        (await _information.UpdateAsync(id, request, ct)).ToActionResult(Ok);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _information.DeleteAsync(id, ct)).ToActionResult(NoContent);

    [HttpPost("{id:guid}/keywords")]
    public async Task<IActionResult> AddKeyword(Guid id, [FromBody] AddKeywordRequest request, CancellationToken ct) =>
        (await _information.AddKeywordAsync(id, request.Text, ct)).ToActionResult(Ok);

    [HttpDelete("{id:guid}/keywords/{keywordId:guid}")]
    public async Task<IActionResult> DeleteKeyword(Guid id, Guid keywordId, CancellationToken ct) =>
        (await _information.DeleteKeywordAsync(id, keywordId, ct)).ToActionResult(NoContent);
}

public class AddKeywordRequest
{
    public required string Text { get; set; }
}
