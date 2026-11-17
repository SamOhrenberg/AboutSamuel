using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;

namespace PortfolioWebsite.Api.Services.Admin;

public record AdminKeywordDto(Guid KeywordId, string Text);

/// <summary>Information entries (SamuelLM's knowledge snippets) and their keywords.</summary>
public class AdminInformationService(SqlDbContext _db, ILogger<AdminInformationService> _logger)
{
    public async Task<List<AdminInformationDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await _db.Information.Include(i => i.Keywords).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<AdminResult<AdminInformationDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var item = await Load(id, ct);
        return item is null ? AdminFailure.NotFound() : ToDto(item);
    }

    public async Task<AdminInformationDto> CreateAsync(AdminInformationRequest request, CancellationToken ct = default)
    {
        var info = new Information { InformationId = Guid.NewGuid(), Text = request.Text };
        info.Keywords = NewKeywords(request.Keywords, info);

        await _db.Information.AddAsync(info, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin created Information {Id}", info.InformationId);
        return ToDto(info);
    }

    /// <summary>Replaces the text and every keyword, and clears the embedding.</summary>
    public async Task<AdminResult<AdminInformationDto>> UpdateAsync(Guid id, AdminInformationRequest request,
        CancellationToken ct = default)
    {
        var info = await Load(id, ct);
        if (info is null) return AdminFailure.NotFound();

        info.Text = request.Text;
        info.Embedding = null;

        // New keywords are added explicitly. Reached only through the navigation, EF sees a set
        // key on a generated-key entity, takes it for an existing row, and the UPDATE fails.
        _db.Keywords.RemoveRange(info.Keywords);
        info.Keywords = NewKeywords(request.Keywords, info);
        _db.Keywords.AddRange(info.Keywords);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin updated Information {Id}", id);
        return ToDto(info);
    }

    public async Task<AdminResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Keywords cascade-delete via FK, but EF needs them loaded to track them
        var info = await Load(id, ct);
        if (info is null) return AdminFailure.NotFound();

        _db.Information.Remove(info);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin deleted Information {Id}", id);
        return AdminResult.Ok();
    }

    public async Task<AdminResult<AdminKeywordDto>> AddKeywordAsync(Guid id, string? text, CancellationToken ct = default)
    {
        var info = await Load(id, ct);
        if (info is null) return AdminFailure.NotFound();

        if (string.IsNullOrWhiteSpace(text))
            return AdminFailure.Invalid("Keyword text is required.");

        var keyword = new Keyword { KeywordId = Guid.NewGuid(), Text = text.Trim(), Information = info };
        _db.Keywords.Add(keyword);  // explicitly, for the same reason as in UpdateAsync
        await _db.SaveChangesAsync(ct);

        return new AdminKeywordDto(keyword.KeywordId, keyword.Text);
    }

    public async Task<AdminResult> DeleteKeywordAsync(Guid id, Guid keywordId, CancellationToken ct = default)
    {
        var keyword = await _db.Keywords
            .FirstOrDefaultAsync(k => k.KeywordId == keywordId && k.Information.InformationId == id, ct);
        if (keyword is null) return AdminFailure.NotFound();

        _db.Keywords.Remove(keyword);
        await _db.SaveChangesAsync(ct);
        return AdminResult.Ok();
    }

    private Task<Information?> Load(Guid id, CancellationToken ct) =>
        _db.Information.Include(i => i.Keywords).FirstOrDefaultAsync(i => i.InformationId == id, ct);

    private static List<Keyword> NewKeywords(IEnumerable<string> keywords, Information info) => keywords
        .Where(k => !string.IsNullOrWhiteSpace(k))
        .Select(k => new Keyword { KeywordId = Guid.NewGuid(), Text = k.Trim(), Information = info })
        .ToList();

    private static AdminInformationDto ToDto(Information info) => new()
    {
        InformationId = info.InformationId,
        Text = info.Text,
        Keywords = info.Keywords.Select(k => k.Text).ToList(),
        HasEmbedding = info.Embedding != null,
    };
}
