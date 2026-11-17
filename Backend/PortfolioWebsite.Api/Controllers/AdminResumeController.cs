using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioWebsite.Api.Dtos.Admin;
using PortfolioWebsite.Api.Services.Admin;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>
/// The official resume PDF. Uploading makes the new file current immediately, and the old
/// versions stay as history. The public download is ResumeController.
/// </summary>
[ApiController]
[Route("admin/resume")]
[Authorize(Roles = "Admin")]
public class AdminResumeController(AdminResumeService _resume) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ResumeFileDto>> GetVersions(CancellationToken ct) => await _resume.ListVersionsAsync(ct);

    [HttpPost]
    [RequestSizeLimit(AdminResumeService.MaxBytes + 64 * 1024)]  // the file plus multipart overhead
    public async Task<ActionResult<ResumeFileDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Choose a PDF to upload.");
        if (file.Length > AdminResumeService.MaxBytes) return BadRequest("The PDF is over the 5 MB limit.");

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            content = ms.ToArray();
        }

        var result = await _resume.UploadAsync(file.FileName, content, ct);
        // This endpoint has always answered a bad file with a plain string
        return result.IsOk ? Ok(result.Value) : BadRequest(result.Message);
    }

    /// <summary>Makes an older version current again.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        (await _resume.ActivateAsync(id, ct)).ToActionResult(Ok);
}
