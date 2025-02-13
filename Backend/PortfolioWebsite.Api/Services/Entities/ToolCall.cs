using Newtonsoft.Json.Linq;

namespace PortfolioWebsite.Api.Services.Entities;

public class ToolCall
{
    public string Name
    {
        get
        {
            return Function?.Name ?? string.Empty;
        }
    }

    private JObject? _arguments = null;
    public JObject? Arguments
    {
        get
        {
            if (_arguments is not null)
            {
                return _arguments;
            }

            if (Function is null)
            {
                return null;
            }

            _arguments = JObject.Parse(Function.Arguments);
            return _arguments;
        }
    }

    public record FunctionRec(string Name, string Arguments);

    public FunctionRec? Function { get; set; }

    public ToolCall(string name, string arguments)
    {
        Function = new FunctionRec(name, arguments);
    }
}