using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PortfolioWebsite.Api.Data;

namespace PortfolioWebsite.Api.Controllers;

public record DataTransferTableDto(string Name, long Rows);
public record DataTransferGroupDto(
    string Key, string Label, string Description, string? Warning, bool DefaultSelected, DataTransferTableDto[] Tables);
public record DataTransferInfoDto(bool AllowImport, string LatestMigration, DataTransferGroupDto[] Groups);

/// <summary>
/// Copies data between environments through the admin UI: export a JSON snapshot from one site,
/// import it into another (normally prod into dev). Rows move as raw Postgres jsonb, so there
/// are no per-table DTOs to keep in step with the schema.
///
/// Import wipes the selected tables, so it is off unless DataTransfer:AllowImport is true. Set
/// that on the dev environment only. Export is read-only and always available.
/// </summary>
[ApiController]
[Route("admin/data-transfer")]
[Authorize(Roles = "Admin")]
public class AdminDataTransferController(
    ILogger<AdminDataTransferController> _logger,
    SqlDbContext _db,
    IConfiguration _config) : ControllerBase
{
    private const int FormatVersion = 1;
    private const long MaxImportBytes = 200L * 1024 * 1024;
    private const string ConfirmPhrase = "OVERWRITE";

    private record Group(string Key, string Label, string Description, string? Warning, bool DefaultSelected, string[] Tables);

    // Tables are listed parents first: that is the insert order, and truncate takes them all at once.
    // Every foreign key stays inside one group. AdminTokens is deliberately in none of them.
    private static readonly Group[] Groups =
    [
        new("content", "Site content",
            "Information, keywords, work experience, projects, and the skill map.", null, true,
            ["Information", "Keywords", "WorkExperiences", "Projects", "ProjectWorkExperience", "EmbeddingProjections"]),
        new("resume", "Resume",
            "Resume PDFs, plus the analyses and suggestions made from them.", null, true,
            ["ResumeFiles", "ResumeAnalyses", "ResumeSuggestions"]),
        new("jobfit", "Job Fit runs", "Past Job Fit analyses submitted by visitors.", null, false,
            ["JobFitRuns", "JobFitRequirements"]),
        new("adversarial", "Adversarial test runs", "Results from the adversarial test suite.", null, false,
            ["AdversarialRuns", "AdversarialCaseResults"]),
        new("chats", "Chat logs", "Visitor conversations with the chatbot.",
            "Contains visitors' messages.", false,
            ["Chats"]),
        new("recruiters", "Recruiter triage",
            "Triage settings, postings, and processed emails.",
            "Contains recruiter emails. After import, triage is forced off and shadow mode on so dev can't act on them.", false,
            ["RecruiterTriageSettings", "RecruiterPostings", "RecruiterEmails", "RecruiterPitches"]),
    ];

    private bool AllowImport => _config.GetValue<bool>("DataTransfer:AllowImport");

    [HttpGet("info")]
    public async Task<ActionResult<DataTransferInfoDto>> Info()
    {
        var migrations = (await _db.Database.GetAppliedMigrationsAsync()).ToList();
        var groups = new List<DataTransferGroupDto>();
        foreach (var g in Groups)
        {
            var tables = new List<DataTransferTableDto>();
            foreach (var t in g.Tables)
                tables.Add(new(t, await TableExistsAsync(t) ? await CountRowsAsync(t) : 0));
            groups.Add(new(g.Key, g.Label, g.Description, g.Warning, g.DefaultSelected, tables.ToArray()));
        }
        return new DataTransferInfoDto(AllowImport, migrations.LastOrDefault() ?? "", groups.ToArray());
    }

    /// <summary>Downloads the selected groups as one JSON file. groups is comma separated group keys.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string groups)
    {
        var selected = ParseGroups(groups);
        if (selected.Count == 0) return BadRequest("Pick at least one group to export.");

        _db.Database.SetCommandTimeout(300);
        var migrations = (await _db.Database.GetAppliedMigrationsAsync()).ToList();

        using var ms = new MemoryStream();
        await using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("format", FormatVersion);
            w.WriteString("exportedAt", DateTimeOffset.UtcNow);
            w.WriteStartArray("migrations");
            foreach (var m in migrations) w.WriteStringValue(m);
            w.WriteEndArray();
            w.WriteStartArray("groups");
            foreach (var g in selected) w.WriteStringValue(g.Key);
            w.WriteEndArray();

            w.WriteStartObject("tables");
            foreach (var table in selected.SelectMany(g => g.Tables))
            {
                // A table is missing when this environment hasn't run its migration yet
                if (!await TableExistsAsync(table)) continue;
                w.WritePropertyName(table);
                w.WriteRawValue(await ReadTableJsonAsync(table));
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }

        _logger.LogInformation("Data export: groups {Groups}, {Bytes} bytes", groups, ms.Length);
        var fileName = $"aboutsamuel-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.json";
        return File(ms.ToArray(), "application/json", fileName);
    }

    /// <summary>
    /// Replaces the selected groups with the contents of an export file, all in one transaction,
    /// so a failure leaves the database as it was.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public async Task<IActionResult> Import(IFormFile? file, [FromForm] string groups, [FromForm] string confirm)
    {
        if (!AllowImport)
            return StatusCode(403, new { Message = "Import is disabled on this environment." });
        if (confirm != ConfirmPhrase)
            return BadRequest(new { Message = $"Type {ConfirmPhrase} to confirm." });
        if (file is null || file.Length == 0)
            return BadRequest(new { Message = "Choose an export file." });

        var selected = ParseGroups(groups);
        if (selected.Count == 0)
            return BadRequest(new { Message = "Pick at least one group to import." });

        JsonDocument doc;
        try
        {
            await using var stream = file.OpenReadStream();
            doc = await JsonDocument.ParseAsync(stream);
        }
        catch (JsonException)
        {
            return BadRequest(new { Message = "That file isn't valid JSON." });
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("format", out var fmt) || fmt.GetInt32() != FormatVersion
                || !root.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("migrations", out var fileMigrations))
                return BadRequest(new { Message = "That isn't an AboutSamuel export file." });

            // The file may be from an older schema (this environment ahead is fine), but never a
            // newer one: its columns might not exist here.
            var local = (await _db.Database.GetAppliedMigrationsAsync()).ToHashSet();
            var unknown = fileMigrations.EnumerateArray().Select(m => m.GetString()!).Where(m => !local.Contains(m)).ToList();
            if (unknown.Count > 0)
                return BadRequest(new { Message = $"The export is from a newer schema than this environment. Deploy this environment first (missing {string.Join(", ", unknown)})." });

            var imported = new List<string>();
            var skipped = new List<string>();
            var plan = new List<string>();
            foreach (var g in selected)
            {
                // A group moves whole or not at all: wiping some of its tables breaks the foreign keys
                if (g.Tables.All(t => tables.TryGetProperty(t, out _)) && await AllTablesExistAsync(g))
                {
                    plan.AddRange(g.Tables);
                    imported.Add(g.Label);
                }
                else skipped.Add(g.Label);
            }
            if (plan.Count == 0)
                return BadRequest(new { Message = "None of the selected groups are in that file.", Skipped = skipped });

            _db.Database.SetCommandTimeout(600);
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Table names come from the fixed list above, never from the request
                var quoted = string.Join(", ", plan.Select(t => $"\"{t}\""));
                await _db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {quoted}");

                var rowCounts = new Dictionary<string, int>();
                foreach (var table in plan)
                {
                    var rows = tables.GetProperty(table);
                    rowCounts[table] = rows.GetArrayLength();
                    if (rowCounts[table] == 0) continue;

                    var param = new NpgsqlParameter("data", NpgsqlDbType.Jsonb) { Value = rows.GetRawText() };
                    await _db.Database.ExecuteSqlRawAsync(
                        $"INSERT INTO \"{table}\" SELECT * FROM jsonb_populate_recordset(NULL::\"{table}\", @data)", param);
                }

                // Production's triage settings must never start dev replying to recruiters
                if (plan.Contains("RecruiterTriageSettings"))
                    await _db.Database.ExecuteSqlRawAsync(
                        "UPDATE \"RecruiterTriageSettings\" SET \"TriageEnabled\" = false, \"ShadowMode\" = true");

                await tx.CommitAsync();
                _logger.LogWarning("Data import: replaced {Tables}", string.Join(", ", plan));
                return Ok(new { Message = "Import complete.", Imported = imported, Skipped = skipped, Rows = rowCounts });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Data import failed and was rolled back");
                return StatusCode(500, new { Message = $"Import failed and nothing was changed: {ex.GetBaseException().Message}" });
            }
        }
    }

    private static List<Group> ParseGroups(string? keys)
    {
        var wanted = (keys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Groups.Where(g => wanted.Contains(g.Key)).ToList();
    }

    private async Task<bool> AllTablesExistAsync(Group g)
    {
        foreach (var t in g.Tables)
            if (!await TableExistsAsync(t)) return false;
        return true;
    }

    // Names below are from the fixed Groups list, so interpolating them into SQL is safe
    private async Task<bool> TableExistsAsync(string table) =>
        (await _db.Database.SqlQueryRaw<bool>(
            $"SELECT to_regclass('\"{table}\"') IS NOT NULL AS \"Value\"").ToListAsync()).Single();

    private async Task<long> CountRowsAsync(string table) =>
        (await _db.Database.SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM \"{table}\"").ToListAsync()).Single();

    private async Task<string> ReadTableJsonAsync(string table) =>
        (await _db.Database.SqlQueryRaw<string>(
            $"SELECT coalesce(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text AS \"Value\" FROM \"{table}\" t").ToListAsync()).Single();
}
