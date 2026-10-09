using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Services;

/// <summary>
/// Applies an approved resume suggestion to the site data. The agent only proposes; this is
/// the one place the proposals touch work experience, projects, and information, and it
/// works field by field from an allowlist. The embedding is cleared and rebuilt the same way
/// the admin panel does it, so retrieval stays in step with the text.
/// </summary>
public class ResumeSuggestionApplier(SqlDbContext _db, IEmbeddingService _embeddings, ILogger<ResumeSuggestionApplier> _logger)
{
    public enum Outcome { Applied, NotFound, Conflict, Invalid }

    public record Result(Outcome Outcome, string? Message = null, bool EmbeddingGenerated = false);

    private record Change(JsonElement From, JsonElement To);

    public async Task<Result> ApproveAsync(Guid suggestionId, Dictionary<string, JsonElement>? overrides,
        CancellationToken ct = default)
    {
        var suggestion = await _db.ResumeSuggestions.FirstOrDefaultAsync(s => s.ResumeSuggestionId == suggestionId, ct);
        if (suggestion is null) return new(Outcome.NotFound);
        if (suggestion.Status != "pending")
            return new(Outcome.Conflict, $"This suggestion was already {suggestion.Status}.");

        Dictionary<string, Change> changes;
        try
        {
            changes = JsonSerializer.Deserialize<Dictionary<string, Change>>(suggestion.Changes,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        catch (JsonException)
        {
            return new(Outcome.Invalid, "The suggestion's changes couldn't be read.");
        }

        // An edit before approving can change a proposed value, but never add a field the
        // suggestion didn't propose
        var values = changes.ToDictionary(c => c.Key, c => c.Value.To);
        foreach (var (field, value) in overrides ?? [])
        {
            if (!values.ContainsKey(field)) return new(Outcome.Invalid, $"'{field}' isn't part of this suggestion.");
            values[field] = value;
        }

        Result? failure;
        object entity;
        switch (suggestion.EntityType)
        {
            case "WorkExperience":
                (failure, entity) = await ApplyWork(suggestion, changes, values, ct);
                break;
            case "Project":
                (failure, entity) = await ApplyProject(suggestion, changes, values, ct);
                break;
            case "Information":
                (failure, entity) = await ApplyInformation(suggestion, changes, values, ct);
                break;
            default:
                return new(Outcome.Invalid, $"Unknown entity type '{suggestion.EntityType}'.");
        }
        if (failure is not null) return failure;

        suggestion.Status = "applied";
        suggestion.DecidedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var embedded = await EmbedAsync(entity, ct);
        _logger.LogInformation("Applied resume suggestion {Id}: {Action} {Type} {Label}",
            suggestion.ResumeSuggestionId, suggestion.Action, suggestion.EntityType, suggestion.Label);
        return new(Outcome.Applied, EmbeddingGenerated: embedded);
    }

    public async Task<Result> RejectAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var suggestion = await _db.ResumeSuggestions.FirstOrDefaultAsync(s => s.ResumeSuggestionId == suggestionId, ct);
        if (suggestion is null) return new(Outcome.NotFound);
        if (suggestion.Status != "pending")
            return new(Outcome.Conflict, $"This suggestion was already {suggestion.Status}.");

        suggestion.Status = "rejected";
        suggestion.DecidedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new(Outcome.Applied);
    }

    // ── Work experience ────────────────────────────────────────────────────

    private async Task<(Result?, object)> ApplyWork(ResumeSuggestion s, Dictionary<string, Change> changes,
        Dictionary<string, JsonElement> values, CancellationToken ct)
    {
        WorkExperience row;
        if (s.Action == "update")
        {
            var found = await _db.WorkExperiences.FirstOrDefaultAsync(w => w.WorkExperienceId == s.EntityId, ct);
            if (found is null) return (new Result(Outcome.Conflict, "That work experience no longer exists."), null!);
            row = found;
            if (Stale(changes, f => CurrentWork(row, f)) is { } stale) return (stale, null!);
        }
        else
        {
            // Newest roles come first on the site, so a new one goes to the top
            var top = await _db.WorkExperiences.MinAsync(w => (int?)w.DisplayOrder, ct) ?? 1;
            row = new WorkExperience { WorkExperienceId = Guid.NewGuid(), DisplayOrder = top - 1, IsActive = true };
            _db.WorkExperiences.Add(row);
        }

        foreach (var (field, value) in values)
        {
            var error = field switch
            {
                "employer" => Required(value, v => row.Employer = v, field),
                "title" => Required(value, v => row.Title = v, field),
                "startYear" => Optional(value, v => row.StartYear = v, field),
                "endYear" => Optional(value, v => row.EndYear = v, field),
                "summary" => Optional(value, v => row.Summary = v, field),
                "achievements" => List(value, v => row.Achievements = JsonSerializer.Serialize(v), field),
                _ => $"'{field}' can't be applied to work experience.",
            };
            if (error is not null) return (new Result(Outcome.Invalid, error), null!);
        }
        if (string.IsNullOrWhiteSpace(row.Employer) || string.IsNullOrWhiteSpace(row.Title))
            return (new Result(Outcome.Invalid, "A work experience needs an employer and a title."), null!);

        row.Embedding = null;
        return (null, row);
    }

    private static object? CurrentWork(WorkExperience w, string field) => field switch
    {
        "employer" => w.Employer,
        "title" => w.Title,
        "startYear" => w.StartYear,
        "endYear" => w.EndYear,
        "summary" => w.Summary,
        "achievements" => ParseList(w.Achievements),
        _ => null,
    };

    // ── Projects ───────────────────────────────────────────────────────────

    private async Task<(Result?, object)> ApplyProject(ResumeSuggestion s, Dictionary<string, Change> changes,
        Dictionary<string, JsonElement> values, CancellationToken ct)
    {
        Project row;
        if (s.Action == "update")
        {
            // The work experiences a project belongs to are never touched by a suggestion
            var found = await _db.Projects.Include(p => p.WorkExperiences)
                .FirstOrDefaultAsync(p => p.ProjectId == s.EntityId, ct);
            if (found is null) return (new Result(Outcome.Conflict, "That project no longer exists."), null!);
            row = found;
            if (Stale(changes, f => CurrentProject(row, f)) is { } stale) return (stale, null!);
        }
        else
        {
            var last = await _db.Projects.MaxAsync(p => (int?)p.DisplayOrder, ct) ?? -1;
            row = new Project { ProjectId = Guid.NewGuid(), DisplayOrder = last + 1, IsActive = true };
            _db.Projects.Add(row);
        }

        foreach (var (field, value) in values)
        {
            var error = field switch
            {
                "title" => Required(value, v => row.Title = v, field),
                "role" => Required(value, v => row.Role = v, field),
                "summary" => Required(value, v => row.Summary = v, field),
                "detail" => Optional(value, v => row.Detail = v, field),
                "impactStatement" => Optional(value, v => row.ImpactStatement = v, field),
                "techStack" => List(value, v => row.TechStack = JsonSerializer.Serialize(v), field),
                "startYear" => Optional(value, v => row.StartYear = v, field),
                "endYear" => Optional(value, v => row.EndYear = v, field),
                _ => $"'{field}' can't be applied to a project.",
            };
            if (error is not null) return (new Result(Outcome.Invalid, error), null!);
        }
        if (string.IsNullOrWhiteSpace(row.Title) || string.IsNullOrWhiteSpace(row.Role) || string.IsNullOrWhiteSpace(row.Summary))
            return (new Result(Outcome.Invalid, "A project needs a title, a role, and a summary."), null!);

        row.Embedding = null;
        return (null, row);
    }

    private static object? CurrentProject(Project p, string field) => field switch
    {
        "title" => p.Title,
        "role" => p.Role,
        "summary" => p.Summary,
        "detail" => p.Detail,
        "impactStatement" => p.ImpactStatement,
        "techStack" => ParseList(p.TechStack),
        "startYear" => p.StartYear,
        "endYear" => p.EndYear,
        _ => null,
    };

    // ── Information ────────────────────────────────────────────────────────

    private async Task<(Result?, object)> ApplyInformation(ResumeSuggestion s, Dictionary<string, Change> changes,
        Dictionary<string, JsonElement> values, CancellationToken ct)
    {
        Information row;
        if (s.Action == "update")
        {
            var found = await _db.Information.Include(i => i.Keywords)
                .FirstOrDefaultAsync(i => i.InformationId == s.EntityId, ct);
            if (found is null) return (new Result(Outcome.Conflict, "That information entry no longer exists."), null!);
            row = found;
            if (Stale(changes, f => f switch
                {
                    "text" => row.Text,
                    "keywords" => row.Keywords.Select(k => k.Text).ToList(),
                    _ => null,
                }) is { } stale) return (stale, null!);
        }
        else
        {
            row = new Information { InformationId = Guid.NewGuid() };
            _db.Information.Add(row);
        }

        foreach (var (field, value) in values)
        {
            string? error;
            switch (field)
            {
                case "text":
                    error = Required(value, v => row.Text = v, field);
                    break;
                case "keywords":
                    error = List(value, keywords =>
                    {
                        // Replace the keywords entirely, the way the admin editor does
                        _db.Keywords.RemoveRange(row.Keywords.Where(k => _db.Entry(k).State != EntityState.Added));
                        row.Keywords = keywords
                            .Select(k => new Keyword { KeywordId = Guid.NewGuid(), Text = k, Information = row })
                            .ToList();
                    }, field);
                    break;
                default:
                    error = $"'{field}' can't be applied to information.";
                    break;
            }
            if (error is not null) return (new Result(Outcome.Invalid, error), null!);
        }
        if (string.IsNullOrWhiteSpace(row.Text))
            return (new Result(Outcome.Invalid, "An information entry needs text."), null!);

        row.Embedding = null;
        return (null, row);
    }

    // ── Shared ─────────────────────────────────────────────────────────────

    /// <summary>
    /// For an update, every field must still hold the value the analysis saw. If the site
    /// changed since, applying the suggestion could undo that edit.
    /// </summary>
    private static Result? Stale(Dictionary<string, Change> changes, Func<string, object?> current)
    {
        var stale = changes.Where(c => !Matches(c.Value.From, current(c.Key))).Select(c => c.Key).ToList();
        return stale.Count == 0 ? null : new Result(Outcome.Conflict,
            $"The site changed since this analysis ({string.Join(", ", stale)}). Reject this one and analyze again.");
    }

    private static bool Matches(JsonElement from, object? current) => from.ValueKind switch
    {
        JsonValueKind.Null => current is null || current is "" || current is List<string> { Count: 0 },
        JsonValueKind.String => current is string s && s == from.GetString(),
        JsonValueKind.Array => current is List<string> list &&
                               list.SequenceEqual(from.EnumerateArray().Select(e => e.GetString() ?? "")),
        _ => false,
    };

    private static List<string> ParseList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? Required(JsonElement value, Action<string> set, string field)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            return $"'{field}' needs a value.";
        set(value.GetString()!.Trim());
        return null;
    }

    private static string? Optional(JsonElement value, Action<string?> set, string field)
    {
        if (value.ValueKind == JsonValueKind.Null) { set(null); return null; }
        if (value.ValueKind != JsonValueKind.String) return $"'{field}' must be text.";
        var text = value.GetString()?.Trim();
        set(string.IsNullOrEmpty(text) ? null : text);
        return null;
    }

    private static string? List(JsonElement value, Action<List<string>> set, string field)
    {
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(e => e.ValueKind != JsonValueKind.String))
            return $"'{field}' must be a list of text.";
        set(value.EnumerateArray().Select(e => e.GetString()!.Trim()).Where(t => t.Length > 0).ToList());
        return null;
    }

    /// <summary>Rebuilds the embedding for the changed row. False if the embedding service failed.</summary>
    private async Task<bool> EmbedAsync(object entity, CancellationToken ct)
    {
        string? text;
        switch (entity)
        {
            case WorkExperience w:
                text = ChatService.BuildWorkRagText(w);
                break;
            case Project p:
                await _db.Entry(p).Collection(x => x.WorkExperiences).LoadAsync(ct);
                text = ChatService.BuildProjectRagText(p);
                break;
            case Information i:
                text = i.Text;
                break;
            default:
                return false;
        }
        if (string.IsNullOrWhiteSpace(text)) return false;

        var vector = await _embeddings.GetEmbeddingAsync(text);
        if (vector is null)
        {
            _logger.LogWarning("Embedding failed after applying a resume suggestion; run Generate Embeddings");
            return false;
        }

        switch (entity)
        {
            case WorkExperience w: w.Embedding = new Vector(vector); break;
            case Project p: p.Embedding = new Vector(vector); break;
            case Information i: i.Embedding = new Vector(vector); break;
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
