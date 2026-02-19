using System.Text.Json.Nodes;

namespace PortfolioWebsite.Api.Services.Entities;

public class ToolCall
{
    public string Name { get; set; } = string.Empty;
    public JsonObject? Arguments { get; set; }

    public ToolCall()
    {
    }

    public ToolCall(string name, string arguments)
    {
        Name = name;
        Arguments = JsonNode.Parse(arguments)?.AsObject();
    }
}