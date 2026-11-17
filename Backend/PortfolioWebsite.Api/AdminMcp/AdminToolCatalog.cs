using System.Reflection;
using ModelContextProtocol.Server;
using PortfolioWebsite.Api.AdminMcp.Tools;

namespace PortfolioWebsite.Api.AdminMcp;

/// <summary>
/// The admin tools, built once at startup. They are never registered with WithTools, so the
/// server's default tool set stays the public one; AdminMcpRegistration hands these out only
/// to requests on /mcp/admin from an authenticated admin.
/// </summary>
public sealed class AdminToolCatalog
{
    /// <summary>Every admin tool's name starts with this, which keeps them apart from the
    /// public tools and lets the guard filters recognise one by name alone.</summary>
    public const string NamePrefix = "admin_";

    public static readonly Type[] ToolTypes = [typeof(AdminContentTools), typeof(AdminOperationsTools), typeof(AdminActivityTools)];

    private readonly McpServerTool[] _tools;
    private readonly HashSet<string> _names;

    public AdminToolCatalog(IServiceProvider services)
    {
        _tools = ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Select(method => McpServerTool.Create(method,
                    context => ActivatorUtilities.CreateInstance(context.Services!, type),
                    new McpServerToolCreateOptions { Services = services })))
            .ToArray();

        var badName = _tools.FirstOrDefault(t => !t.ProtocolTool.Name.StartsWith(NamePrefix, StringComparison.Ordinal));
        if (badName is not null)
            throw new InvalidOperationException($"Admin tool '{badName.ProtocolTool.Name}' must start with '{NamePrefix}'.");

        _names = _tools.Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);
        if (_names.Count != _tools.Length)
            throw new InvalidOperationException("Two admin tools share a name.");
    }

    public IReadOnlyList<McpServerTool> Tools => _tools;

    /// <summary>A fresh collection for one request's server, holding the shared tool instances.</summary>
    public McpServerPrimitiveCollection<McpServerTool> CreateCollection()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in _tools)
            collection.Add(tool);
        return collection;
    }

    /// <summary>True for any name that is, or claims to be, an admin tool.</summary>
    public static bool IsAdminToolName(string? name) =>
        name is not null && name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase);
}
