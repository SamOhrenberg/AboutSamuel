
using System.Text.Json.Serialization;

namespace PortfolioWebsite.Api.Services.Entities.Google;

public class FunctionCallingConfig
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}