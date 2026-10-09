using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PortfolioWebsite.Api.Data;

namespace PortfolioWebsite.Api.Controllers;

/// <summary>The public download of the current official resume PDF.</summary>
[ApiController]
[Route("resume")]
public class ResumeController(SqlDbContext _db) : ControllerBase
{
    public const string DownloadName = "Samuel_Ohrenberg_Resume.pdf";

    [HttpGet("official")]
    public async Task<IActionResult> GetOfficial()
    {
        // The hash first, so a browser revalidating its cached copy doesn't pull the bytes
        var sha = await _db.ResumeFiles.AsNoTracking().Where(r => r.IsCurrent).Select(r => r.Sha256)
            .FirstOrDefaultAsync();
        if (sha is null) return NotFound();

        var etag = new EntityTagHeaderValue($"\"{sha}\"");
        Response.Headers.CacheControl = "public, max-age=300";
        if (Request.GetTypedHeaders().IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: true)))
            return StatusCode(StatusCodes.Status304NotModified);

        var content = await _db.ResumeFiles.AsNoTracking().Where(r => r.Sha256 == sha && r.IsCurrent)
            .Select(r => r.Content).FirstOrDefaultAsync();
        if (content is null) return NotFound();  // swapped between the two queries; the next request is fine

        Response.Headers.ETag = etag.ToString();
        Response.Headers.ContentDisposition = $"inline; filename=\"{DownloadName}\"";
        return File(content, "application/pdf");
    }
}
