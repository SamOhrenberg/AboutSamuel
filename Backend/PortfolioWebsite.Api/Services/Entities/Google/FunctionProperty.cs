using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class FunctionProperty
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
}
