using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;

namespace PortfolioWebsite.Api.Controllers;

[ApiController]
[Route("admin/information")]
[Authorize(Roles = "Admin")]
public class AdminInformationController(
    ILogger<AdminInformationController> _logger,
    SqlDbContext _db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<AdminInformationDto>> GetAll()
    {
        _logger.LogInformation("Admin GET /admin/information");
        var items = await _db.Information.Include(i => i.Keywords).ToListAsync();
        return items.Select(ToDto);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _db.Information
            .Include(i => i.Keywords)
            .FirstOrDefaultAsync(i => i.InformationId == id);

        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminInformationRequest request)
    {
        var info = new Information
        {
            InformationId = Guid.NewGuid(),
            Text = request.Text,
            Keywords = request.Keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => new Keyword { KeywordId = Guid.NewGuid(), Text = k.Trim() })
                .ToList()
        };

        // Wire up the navigation property
        foreach (var kw in info.Keywords) kw.Information = info;

        await _db.Information.AddAsync(info);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin created Information {Id}", info.InformationId);
        return Ok(ToDto(info));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AdminInformationRequest request)
    {
        var info = await _db.Information
            .Include(i => i.Keywords)
            .FirstOrDefaultAsync(i => i.InformationId == id);

        if (info is null) return NotFound();

        info.Text = request.Text;
        info.EmbeddingJson = null;

        // Replace keywords entirely — remove old, add new
        _db.Keywords.RemoveRange(info.Keywords);
        info.Keywords = request.Keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => new Keyword
            {
                KeywordId = Guid.NewGuid(),
                Text = k.Trim(),
                Information = info
            })
            .ToList();

        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin updated Information {Id}", id);
        return Ok(ToDto(info));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var info = await _db.Information
            .Include(i => i.Keywords)
            .FirstOrDefaultAsync(i => i.InformationId == id);

        if (info is null) return NotFound();

        // Keywords cascade-delete via FK, but EF needs them loaded to track them
        _db.Information.Remove(info);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin deleted Information {Id}", id);
        return NoContent();
    }

    // ── Keywords sub-resource ─────────────────────────────────────────────

    [HttpPost("{id:guid}/keywords")]
    public async Task<IActionResult> AddKeyword(Guid id, [FromBody] AddKeywordRequest request)
    {
        var info = await _db.Information.Include(i => i.Keywords)
            .FirstOrDefaultAsync(i => i.InformationId == id);

        if (info is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { Message = "Keyword text is required." });

        var keyword = new Keyword
        {
            KeywordId = Guid.NewGuid(),
            Text = request.Text.Trim(),
            Information = info
        };

        info.Keywords.Add(keyword);
        await _db.SaveChangesAsync();

        return Ok(new { keyword.KeywordId, keyword.Text });
    }

    [HttpDelete("{id:guid}/keywords/{keywordId:guid}")]
    public async Task<IActionResult> DeleteKeyword(Guid id, Guid keywordId)
    {
        var keyword = await _db.Keywords
            .FirstOrDefaultAsync(k => k.KeywordId == keywordId && k.Information.InformationId == id);

        if (keyword is null) return NotFound();

        _db.Keywords.Remove(keyword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static AdminInformationDto ToDto(Information info) => new()
    {
        InformationId = info.InformationId,
        Text = info.Text,
        Keywords = info.Keywords.Select(k => k.Text).ToList()
    };
}

public class AddKeywordRequest
{
    public required string Text { get; set; }
}