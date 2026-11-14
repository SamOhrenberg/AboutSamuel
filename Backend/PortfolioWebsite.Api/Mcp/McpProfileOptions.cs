using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// The hand-written "about me" for get_profile, bound from the committed Mcp/profile.json.
/// Blank fields come back as null or empty rather than placeholder text.
/// </summary>
public sealed class McpProfileOptions
{
    public const string SectionName = "McpProfile";
    public const string FilePath = "Mcp/profile.json";

    public string Name { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> CurrentFocus { get; set; } = [];
    public string? Availability { get; set; }
    public List<ProfileLinkOptions> Links { get; set; } = [];

    public ProfileResult ToResult(PortfolioUrls urls) => new(
        Name.Trim(),
        Headline.Trim(),
        Summary.Trim(),
        CurrentFocus.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList(),
        string.IsNullOrWhiteSpace(Availability) ? null : Availability.Trim(),
        Links
            .Where(l => !string.IsNullOrWhiteSpace(l.Label) && Uri.TryCreate(l.Url, UriKind.Absolute, out _))
            .Select(l => new ProfileLink(l.Label.Trim(), l.Url.Trim()))
            .ToList(),
        urls.Page("/"));
}

public sealed class ProfileLinkOptions
{
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
}
