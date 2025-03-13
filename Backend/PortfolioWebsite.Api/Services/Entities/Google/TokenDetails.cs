using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class TokenDetails
{
    [JsonPropertyName("modality")] public string Modality { get; set; }
    [JsonPropertyName("tokenCount")] public int TokenCount { get; set; }
}
