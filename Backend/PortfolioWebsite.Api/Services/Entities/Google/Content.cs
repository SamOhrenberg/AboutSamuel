using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class Content
{
    [JsonPropertyName("role")] public string Role { get; set; }
    [JsonPropertyName("parts")] public List<Part> Parts { get; set; }
}
