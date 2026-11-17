using System.Text.Json;

namespace PortfolioWebsite.Api.Dtos.Admin;

public record ResumeFileDto(
    Guid ResumeFileId, string FileName, int SizeBytes, string Sha256, bool IsCurrent, bool Analyzed,
    DateTimeOffset UploadedAt);

public record AnalyzeRequest(Guid? ResumeFileId);

public record ApproveSuggestionRequest(Dictionary<string, JsonElement>? Overrides);

public record ResumeAnalysisDto(
    Guid ResumeAnalysisId, Guid ResumeFileId, string ResumeFileName, string Status, string Model, string? Summary,
    string? Error, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int Pending, int Applied, int Rejected);

public record ResumeSuggestionDto(
    Guid ResumeSuggestionId, string Action, string EntityType, Guid? EntityId, string Label, JsonElement Changes,
    string Rationale, string Evidence, string Status, DateTimeOffset? DecidedAt);

public record ResumeAnalysisDetailDto(ResumeAnalysisDto Analysis, IEnumerable<ResumeSuggestionDto> Suggestions);
