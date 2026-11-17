using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Dtos.Admin;
using static PortfolioWebsite.Api.Services.ResumeSuggestionApplier;

namespace PortfolioWebsite.Api.Services.Admin;

public record ResumeTextDto(ResumeFileDto File, string? ExtractedText);

/// <summary>
/// The official resume PDF and the analyses made from it. Uploading makes the new file current
/// immediately and the old versions stay as history. Suggestions from an analysis reach the
/// site only through ResumeSuggestionApplier, once approved.
/// </summary>
public class AdminResumeService(
    SqlDbContext _db,
    AgentServiceClient _agentClient,
    ResumeSuggestionApplier _applier,
    ILogger<AdminResumeService> _logger)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    public Task<List<ResumeFileDto>> ListVersionsAsync(CancellationToken ct = default) =>
        _db.ResumeFiles.AsNoTracking()
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new ResumeFileDto(r.ResumeFileId, r.FileName, r.SizeBytes, r.Sha256, r.IsCurrent,
                r.ExtractedText != null, r.UploadedAt))
            .ToListAsync(ct);

    /// <summary>A version's text as the analysis extracted it. Null until it has been analyzed.</summary>
    public async Task<AdminResult<ResumeTextDto>> GetTextAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.ResumeFiles.AsNoTracking()
            .Where(r => r.ResumeFileId == id)
            .Select(r => new { r.ResumeFileId, r.FileName, r.SizeBytes, r.Sha256, r.IsCurrent, r.ExtractedText, r.UploadedAt })
            .FirstOrDefaultAsync(ct);
        if (row is null) return AdminFailure.NotFound();

        return new ResumeTextDto(
            new ResumeFileDto(row.ResumeFileId, row.FileName, row.SizeBytes, row.Sha256, row.IsCurrent,
                row.ExtractedText != null, row.UploadedAt),
            row.ExtractedText);
    }

    /// <summary>Stores a new PDF and makes it current. The same bytes as the current one is a no-op.</summary>
    public async Task<AdminResult<ResumeFileDto>> UploadAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        if (content.Length == 0) return AdminFailure.Invalid("Choose a PDF to upload.");
        if (content.Length > MaxBytes) return AdminFailure.Invalid("The PDF is over the 5 MB limit.");
        // The extension and content type come from the client, so check the bytes
        if (!content.AsSpan().StartsWith(PdfMagic)) return AdminFailure.Invalid("That file isn't a PDF.");

        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        var current = await _db.ResumeFiles.AsNoTracking().FirstOrDefaultAsync(r => r.IsCurrent, ct);
        if (current?.Sha256 == sha) return ToDto(current);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // Retire the old one first: the unique index allows only one current row
        await _db.ResumeFiles.Where(r => r.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCurrent, false), ct);

        var row = new ResumeFile
        {
            ResumeFileId = Guid.NewGuid(),
            FileName = Path.GetFileName(fileName),
            Content = content,
            SizeBytes = content.Length,
            Sha256 = sha,
            IsCurrent = true,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        _db.ResumeFiles.Add(row);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Resume uploaded: {FileName}, {Size} bytes", row.FileName, row.SizeBytes);
        return ToDto(row);
    }

    /// <summary>Makes an older version current again.</summary>
    public async Task<AdminResult<ResumeFileDto>> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.ResumeFiles.FirstOrDefaultAsync(r => r.ResumeFileId == id, ct);
        if (row is null) return AdminFailure.NotFound();
        if (row.IsCurrent) return ToDto(row);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.ResumeFiles.Where(r => r.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCurrent, false), ct);
        row.IsCurrent = true;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Resume version {ResumeFileId} made current", id);
        return ToDto(row);
    }

    public async Task<List<ResumeAnalysisDto>> ListAnalysesAsync(int take, CancellationToken ct = default)
    {
        var analyses = await _db.ResumeAnalyses.AsNoTracking()
            .Include(a => a.ResumeFile)
            .Include(a => a.Suggestions)
            .OrderByDescending(a => a.StartedAt)
            .Take(Math.Clamp(take, 1, 50))
            .AsSplitQuery()
            .ToListAsync(ct);
        return analyses.Select(ToDto).ToList();
    }

    public async Task<AdminResult<ResumeAnalysisDetailDto>> GetAnalysisAsync(Guid id, CancellationToken ct = default)
    {
        var analysis = await _db.ResumeAnalyses.AsNoTracking()
            .Include(a => a.ResumeFile)
            .Include(a => a.Suggestions)
            .FirstOrDefaultAsync(a => a.ResumeAnalysisId == id, ct);
        if (analysis is null) return AdminFailure.NotFound();

        return new ResumeAnalysisDetailDto(ToDto(analysis), analysis.Suggestions
            .OrderBy(s => s.Status == "pending" ? 0 : 1)
            .ThenBy(s => s.DisplayOrder)
            .Select(ToDto)
            .ToList());
    }

    /// <summary>The agent service's progress events for a new analysis, as raw JSON.</summary>
    public IAsyncEnumerable<string> StreamAnalysisAsync(Guid? resumeFileId, CancellationToken ct = default)
    {
        _logger.LogInformation("Admin started a resume analysis");
        return _agentClient.StreamResumeAnalysisAsync(resumeFileId, ct);
    }

    /// <summary>
    /// Runs an analysis to the end and returns what it saved. For callers that can't show the
    /// progress stream. Fails with the agent's error message when it sends one.
    /// </summary>
    public async Task<AdminResult<ResumeAnalysisDetailDto>> RunAnalysisAsync(Guid? resumeFileId, CancellationToken ct = default)
    {
        // The agent saves the analysis and stamps it with the database clock, so find the new
        // one by comparing the newest analysis before and after
        var before = await NewestAnalysisIdAsync(ct);
        string? error = null;

        await foreach (var payload in StreamAnalysisAsync(resumeFileId, ct))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("error", out var e)
                    && e.ValueKind == JsonValueKind.String)
                    error = e.GetString();
            }
            catch (JsonException)
            {
                // Progress events are informational; one that doesn't parse doesn't matter
            }
        }

        var latest = await NewestAnalysisIdAsync(ct);
        if (latest is null || latest == before)
            return AdminFailure.Unavailable(error ?? "The analysis didn't save a result.");
        return await GetAnalysisAsync(latest.Value, ct);
    }

    private Task<Guid?> NewestAnalysisIdAsync(CancellationToken ct) =>
        _db.ResumeAnalyses.AsNoTracking()
            .OrderByDescending(a => a.StartedAt)
            .Select(a => (Guid?)a.ResumeAnalysisId)
            .FirstOrDefaultAsync(ct);

    public async Task<AdminResult<bool>> ApproveSuggestionAsync(Guid id, Dictionary<string, JsonElement>? overrides,
        CancellationToken ct = default)
    {
        var result = await _applier.ApproveAsync(id, overrides, ct);
        return result.Outcome switch
        {
            Outcome.Applied => result.EmbeddingGenerated,
            Outcome.NotFound => AdminFailure.NotFound(),
            Outcome.Conflict => AdminFailure.Conflict(result.Message ?? "Conflict."),
            _ => AdminFailure.Invalid(result.Message ?? "Invalid."),
        };
    }

    public async Task<AdminResult> RejectSuggestionAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _applier.RejectAsync(id, ct);
        return result.Outcome switch
        {
            Outcome.Applied => AdminResult.Ok(),
            Outcome.NotFound => AdminFailure.NotFound(),
            _ => AdminFailure.Conflict(result.Message ?? "Conflict."),
        };
    }

    private static ResumeFileDto ToDto(ResumeFile r) =>
        new(r.ResumeFileId, r.FileName, r.SizeBytes, r.Sha256, r.IsCurrent, r.ExtractedText != null, r.UploadedAt);

    private static ResumeAnalysisDto ToDto(ResumeAnalysis a) => new(
        a.ResumeAnalysisId, a.ResumeFileId, a.ResumeFile.FileName, a.Status, a.Model, a.Summary, a.Error,
        a.StartedAt, a.CompletedAt,
        a.Suggestions.Count(s => s.Status == "pending"),
        a.Suggestions.Count(s => s.Status == "applied"),
        a.Suggestions.Count(s => s.Status == "rejected"));

    private static ResumeSuggestionDto ToDto(ResumeSuggestion s) => new(
        s.ResumeSuggestionId, s.Action, s.EntityType, s.EntityId, s.Label,
        JsonSerializer.Deserialize<JsonElement>(s.Changes), s.Rationale, s.Evidence, s.Status, s.DecidedAt);
}
