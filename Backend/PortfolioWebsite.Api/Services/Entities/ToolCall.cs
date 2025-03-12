using Newtonsoft.Json.Linq;

namespace PortfolioWebsite.Api.Services.Entities;

public class ToolCall
{
    public string Name { get; set; }

    public JObject? Arguments { get; set; }

    public ToolCall()
    {
    }

    public ToolCall(string name, string arguments)
    {
        Name = name;
        Arguments = JObject.Parse(arguments);
    }
}