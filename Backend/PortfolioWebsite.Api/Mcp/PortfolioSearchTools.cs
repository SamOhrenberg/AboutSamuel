using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Server;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Search tools for the public MCP server. Same annotations as PortfolioTools. They read
/// only, and the one outbound call (query embeddings) is internal and capped, so they're
/// still closed-world from the client's point of view.
/// </summary>
[McpServerToolType]
public sealed class PortfolioSearchTools(IPortfolioSearchService search)
{
    [McpServerTool(Name = "search_portfolio", Title = "Search the portfolio",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Searches Samuel's projects, work experience and notes (short free-text facts he
        wrote about himself) for a topic, skill or question, most relevant first. Each result
        has its type, a snippet, a score and a url to cite; projects also have a slug for
        get_project. mode is "semantic" (meaning-based) or "fulltext" (keyword fallback when
        semantic search is unavailable; rephrase with concrete keywords if it finds nothing).
        Scores only rank results within one response. For several requirements at once, use
        find_evidence instead.
        """)]
    public Task<SearchResult> SearchPortfolio(
        [Description("What to look for, in plain language, for example \"event-driven systems on Azure\". At most 300 characters.")]
        [MaxLength(McpInput.MaxQueryLength)]
        string query,
        [Description("Maximum results, 1 to 10.")]
        [Range(1, McpInput.MaxSearchLimit)]
        int limit = 5,
        [Description("Only these result types: any of \"project\", \"experience\", \"note\". Omit for all.")]
        [MaxLength(3)]
        string[]? types = null,
        CancellationToken cancellationToken = default)
    {
        var text = McpInput.RequiredText(query, nameof(query), McpInput.MaxQueryLength);
        var max = McpInput.Range(limit, nameof(limit), 1, McpInput.MaxSearchLimit);
        var only = McpInput.OneOf(types, nameof(types), SearchTypes.All);

        return search.SearchAsync(text, max, only, cancellationToken);
    }

    [McpServerTool(Name = "find_evidence", Title = "Find evidence for requirements",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        For each requirement (for example the skills and responsibilities pulled from a job
        description), returns the best-matching projects, work experience and notes, each
        with a snippet, a score and a url to cite. It only retrieves: it doesn't judge
        whether a match is strong, so read the snippets (and get_project for depth) before
        concluding. A requirement whose matches are all off-topic has no evidence. Up to 15
        requirements of at most 300 characters each; blanks and duplicates are dropped. mode
        is "semantic" or "fulltext", as in search_portfolio.
        """)]
    public Task<EvidenceResult> FindEvidence(
        [Description("Short requirements, one per item, for example \"Production experience with PostgreSQL\". 1 to 15 items.")]
        [MaxLength(McpInput.MaxRequirements)]
        string[] requirements,
        [Description("Matches per requirement, 1 to 3.")]
        [Range(1, McpInput.MaxPerRequirement)]
        int perRequirement = 3,
        CancellationToken cancellationToken = default)
    {
        var items = McpInput.TextList(requirements, nameof(requirements), McpInput.MaxRequirements,
            McpInput.MaxRequirementLength);
        var per = McpInput.Range(perRequirement, nameof(perRequirement), 1, McpInput.MaxPerRequirement);

        return search.FindEvidenceAsync(items, per, cancellationToken);
    }
}
