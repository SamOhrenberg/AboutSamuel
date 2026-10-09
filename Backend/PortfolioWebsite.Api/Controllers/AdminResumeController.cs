using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Controllers;

public record ResumeFileDto(
    Guid ResumeFileId, string FileName, int SizeBytes, string Sha256, bool IsCurrent, bool Analyzed,
    DateTimeOffset UploadedAt);

/// <summary>
/// The official resume PDF. Uploading makes the new file current immediately, and the old
/// versions stay as history. The public download is ResumeController.
/// </summary>
[ApiController]
[Route("admin/resume")]
[Authorize(Roles = "Admin")]
public class AdminResumeController(ILogger<AdminResumeController> _logger, SqlDbContext _db) : ControllerBase
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    [HttpGet]
    public async Task<IEnumerable<ResumeFileDto>> GetVersions() =>
        await _db.ResumeFiles.AsNoTracking()
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new ResumeFileDto(r.ResumeFileId, r.FileName, r.SizeBytes, r.Sha256, r.IsCurrent,
                r.ExtractedText != null, r.UploadedAt))
            .ToListAsync();

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]  // the file plus multipart overhead
    public async Task<ActionResult<ResumeFileDto>> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest("Choose a PDF to upload.");
        if (file.Length > MaxBytes) return BadRequest("The PDF is over the 5 MB limit.");

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }
        // The extension and content type come from the client, so check the bytes
        if (!content.AsSpan().StartsWith(PdfMagic)) return BadRequest("That file isn't a PDF.");

        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        var current = await _db.ResumeFiles.AsNoTracking().FirstOrDefaultAsync(r => r.IsCurrent);
        if (current?.Sha256 == sha) return Ok(ToDto(current));  // same file again: nothing to do

        await using var tx = await _db.Database.BeginTransactionAsync();
        // Retire the old one first: the unique index allows only one current row
        await _db.ResumeFiles.Where(r => r.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCurrent, false));

        var row = new ResumeFile
        {
            ResumeFileId = Guid.NewGuid(),
            FileName = Path.GetFileName(file.FileName),
            Content = content,
            SizeBytes = content.Length,
            Sha256 = sha,
            IsCurrent = true,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        _db.ResumeFiles.Add(row);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Resume uploaded: {FileName}, {Size} bytes", row.FileName, row.SizeBytes);
        return Ok(ToDto(row));
    }

    /// <summary>Makes an older version current again.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ResumeFileDto>> Activate(Guid id)
    {
        var row = await _db.ResumeFiles.FirstOrDefaultAsync(r => r.ResumeFileId == id);
        if (row is null) return NotFound();
        if (row.IsCurrent) return Ok(ToDto(row));

        await using var tx = await _db.Database.BeginTransactionAsync();
        await _db.ResumeFiles.Where(r => r.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCurrent, false));
        row.IsCurrent = true;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(ToDto(row));
    }

    private static ResumeFileDto ToDto(ResumeFile r) =>
        new(r.ResumeFileId, r.FileName, r.SizeBytes, r.Sha256, r.IsCurrent, r.ExtractedText != null, r.UploadedAt);
}
