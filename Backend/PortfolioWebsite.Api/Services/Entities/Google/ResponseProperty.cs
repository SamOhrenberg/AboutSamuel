using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class ResponseProperty
{
    [JsonPropertyName("type")]
    public string Type { get; internal set; }
}
