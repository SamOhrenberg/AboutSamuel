using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class ResponseSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, ResponseProperty> Properties { get; set; }
}