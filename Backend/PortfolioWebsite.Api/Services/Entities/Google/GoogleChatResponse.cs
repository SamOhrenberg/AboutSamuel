using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class GoogleChatResponse
{
    [JsonPropertyName("candidates")] public List<Candidate> Candidates { get; set; }
    [JsonPropertyName("usageMetadata")] public UsageMetadata UsageMetadata { get; set; }
    [JsonPropertyName("modelVersion")] public string ModelVersion { get; set; }
}
