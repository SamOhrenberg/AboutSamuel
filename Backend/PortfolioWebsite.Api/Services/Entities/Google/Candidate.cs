using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class Candidate
{
    [JsonPropertyName("content")] public Content Content { get; set; }
    [JsonPropertyName("finishReason")] public string FinishReason { get; set; }
    [JsonPropertyName("avgLogprobs")] public double AvgLogprobs { get; set; }
}
