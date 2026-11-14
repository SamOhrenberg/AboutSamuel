namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Canonical links back to the public site. The project and work experience pages already
/// scroll to an item given ?id=.
/// </summary>
public sealed class PortfolioUrls
{
    public string SiteBaseUrl { get; }

    public PortfolioUrls(string siteBaseUrl)
    {
        if (!Uri.TryCreate(siteBaseUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException($"Site base URL must be an absolute http(s) URL, got '{siteBaseUrl}'.",
                nameof(siteBaseUrl));

        SiteBaseUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    public string Project(Guid id) => $"{SiteBaseUrl}/projects?id={id}";

    public string Experience(Guid id) => $"{SiteBaseUrl}/work-experience?id={id}";

    public string Page(string path) => $"{SiteBaseUrl}/{path.Trim().TrimStart('/')}";
}
