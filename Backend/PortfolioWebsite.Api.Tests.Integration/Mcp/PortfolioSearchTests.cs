using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Pgvector;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Mcp.Contracts;
using PortfolioWebsite.Api.Services;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;
using static PortfolioWebsite.Api.Tests.Integration.Infrastructure.PortfolioSeed;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// search_portfolio and find_evidence end to end over MCP. Rows get the fake embedder's
/// vector for a known phrase, so searching that phrase is an exact semantic match (score 1).
/// The full-text path is forced the way it happens in prod: the embedding call fails.
/// Query vectors are cached for the whole collection, so each semantic test uses its own
/// phrases or expects the cache.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PortfolioSearchTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SiteBaseUrl = "https://aboutsamuel.com";

    private const string ChatPhrase = "retrieval augmented chat over a portfolio";
    private const string FirstChatPhrase = "the original chat prototype in python";
    private const string BillingPhrase = "payment systems modernisation";
    private const string AcmePhrase = "shipping ai features at a product company";
    private const string NotePhrase = "enjoys building tools for other developers";
    private const string SecretPhrase = "an unreleased systems prototype";
    private const string HiddenCorpPhrase = "an early internship";

    private McpClient _client = null!;
    private FakeEmbeddingService Embedder => (FakeEmbeddingService)factory.Services.GetRequiredService<IEmbeddingService>();

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        await SeedVectorsAsync();
        _client = await McpTestClient.ConnectAsync(factory);
    }

    public async Task DisposeAsync()
    {
        Embedder.Fail = false;
        await _client.DisposeAsync();
    }

    [Fact]
    public async Task Semantic_search_ranks_the_exact_match_first_with_slug_and_url()
    {
        var result = await Search(new() { ["query"] = ChatPhrase });

        Assert.Equal(SearchModes.Semantic, result.Mode);
        Assert.Equal(ChatPhrase, result.Query);
        Assert.Equal(5, result.Count);
        var top = result.Results[0];
        Assert.Equal(SearchTypes.Project, top.Type);
        Assert.Equal(PortfolioChat, top.Id);
        Assert.Equal("portfolio-chat", top.Slug);
        Assert.Equal(1.0, top.Score, 3);
        Assert.Equal($"{SiteBaseUrl}/projects?id={PortfolioChat}", top.Url);
        Assert.Equal("RAG chat about my work. Answers recruiter questions.", top.Snippet);
        Assert.Equal(result.Results.OrderByDescending(r => r.Score), result.Results);
    }

    [Fact]
    public async Task Project_slugs_match_list_projects_for_duplicate_titles()
    {
        var result = await Search(new() { ["query"] = FirstChatPhrase, ["limit"] = 1, ["types"] = new[] { "project" } });

        var hit = Assert.Single(result.Results);
        Assert.Equal(PortfolioChatTwo, hit.Id);
        Assert.Equal("portfolio-chat-2", hit.Slug);
    }

    [Fact]
    public async Task Hidden_rows_are_never_returned_even_with_embeddings()
    {
        var result = await Search(new() { ["query"] = SecretPhrase, ["limit"] = 10 });
        var hidden = await Search(new() { ["query"] = HiddenCorpPhrase, ["limit"] = 10 });

        // Three active projects, two active roles, one note.
        Assert.Equal(6, result.Count);
        Assert.DoesNotContain(result.Results.Concat(hidden.Results), r => r.Id == SecretPrototype || r.Id == HiddenCorp);
    }

    [Fact]
    public async Task Types_filter_limits_results_and_notes_link_to_the_home_page()
    {
        var result = await Search(new() { ["query"] = NotePhrase, ["types"] = new[] { "note" } });

        var note = Assert.Single(result.Results);
        Assert.Equal(SearchTypes.Note, note.Type);
        Assert.Null(note.Slug);
        Assert.Equal("Samuel likes building developer tools.", note.Title);
        Assert.Equal($"{SiteBaseUrl}/", note.Url);
        Assert.Equal(1.0, note.Score, 3);
    }

    [Fact]
    public async Task Experience_hits_combine_summary_and_achievements()
    {
        var result = await Search(new() { ["query"] = AcmePhrase, ["limit"] = 1 });

        var hit = Assert.Single(result.Results);
        Assert.Equal(SearchTypes.Experience, hit.Type);
        Assert.Equal("Senior Engineer at Acme", hit.Title);
        Assert.Equal("Builds AI features. Cut deploy time 60% Led 4 engineers", hit.Snippet);
        Assert.Equal($"{SiteBaseUrl}/work-experience?id={Acme}", hit.Url);
    }

    [Fact]
    public async Task Full_text_is_used_when_embedding_fails()
    {
        Embedder.Fail = true;

        var result = await Search(new() { ["query"] = "who rewrote the billing code?" });

        Assert.Equal(SearchModes.FullText, result.Mode);
        Assert.Equal(BillingRewrite, result.Results[0].Id);
        Assert.Equal("billing-rewrite", result.Results[0].Slug);
        Assert.All(result.Results, r => Assert.True(r.Score > 0));
    }

    [Fact]
    public async Task Full_text_searches_achievements_and_skips_hidden_rows()
    {
        Embedder.Fail = true;

        var deploy = await Search(new() { ["query"] = "deploy time" });
        var hidden = await Search(new() { ["query"] = "Secret Prototype Hidden Corp Rust Intern" });

        Assert.Equal(SearchModes.FullText, deploy.Mode);
        Assert.Equal(Acme, Assert.Single(deploy.Results).Id);
        Assert.DoesNotContain(hidden.Results, r => r.Id == SecretPrototype || r.Id == HiddenCorp);
    }

    [Fact]
    public async Task Full_text_with_no_words_returns_nothing()
    {
        Embedder.Fail = true;

        var result = await Search(new() { ["query"] = "?? !!" });

        Assert.Equal(SearchModes.FullText, result.Mode);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task Find_evidence_embeds_the_requirements_in_one_batch()
    {
        var before = Embedder.BatchCalls;

        var result = await Evidence([BillingPhrase, AcmePhrase, "Kubernetes operators in Go"], perRequirement: 1);

        Assert.Equal(before + 1, Embedder.BatchCalls);
        Assert.Equal(SearchModes.Semantic, result.Mode);
        Assert.Equal([BillingPhrase, AcmePhrase, "Kubernetes operators in Go"], result.Requirements.Select(r => r.Requirement));
        Assert.Equal(BillingRewrite, Assert.Single(result.Requirements[0].Matches).Id);
        Assert.Equal(Acme, Assert.Single(result.Requirements[1].Matches).Id);
        // No threshold: an unrelated requirement still gets its nearest row, with a low score.
        Assert.True(Assert.Single(result.Requirements[2].Matches).Score < 0.5);
    }

    [Fact]
    public async Task Find_evidence_reuses_cached_vectors()
    {
        await Evidence(["cached requirement one", "cached requirement two"]);
        var before = Embedder.BatchCalls;

        // Whitespace is normalized away; case isn't, since it can matter ("Go" vs "go").
        var result = await Evidence([" cached requirement one ", "cached \n requirement two"]);

        Assert.Equal(before, Embedder.BatchCalls);
        Assert.Equal(SearchModes.Semantic, result.Mode);
        Assert.All(result.Requirements, r => Assert.Equal(3, r.Matches.Count));
    }

    [Fact]
    public async Task Find_evidence_falls_back_to_full_text_for_the_whole_call()
    {
        Embedder.Fail = true;

        var result = await Evidence(["Rewrote billing", "Experience with Haskell"]);

        Assert.Equal(SearchModes.FullText, result.Mode);
        Assert.Equal(BillingRewrite, result.Requirements[0].Matches[0].Id);
        Assert.Empty(result.Requirements[1].Matches);
    }

    [Fact]
    public async Task Bad_input_is_a_tool_error()
    {
        var types = await _client.CallExpectingErrorAsync("search_portfolio",
            new() { ["query"] = "x", ["types"] = new[] { "chats" } });
        var tooMany = await _client.CallExpectingErrorAsync("find_evidence",
            new() { ["requirements"] = Enumerable.Range(0, 16).Select(i => $"r{i}").ToArray() });

        Assert.EndsWith(": types may only contain project, experience, note (got 'chats').", types);
        Assert.EndsWith(": requirements may contain at most 15 items (got 16).", tooMany);
    }

    private Task<SearchResult> Search(Dictionary<string, object?> args) =>
        _client.CallStructuredAsync<SearchResult>("search_portfolio", args);

    private Task<EvidenceResult> Evidence(string[] requirements, int perRequirement = 3) =>
        _client.CallStructuredAsync<EvidenceResult>("find_evidence",
            new() { ["requirements"] = requirements, ["perRequirement"] = perRequirement });

    private async Task SeedVectorsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlDbContext>();
        Vector For(string phrase) => new(Embedder.VectorFor(phrase));

        foreach (var (id, phrase) in new[]
                 {
                     (PortfolioChat, ChatPhrase), (PortfolioChatTwo, FirstChatPhrase),
                     (BillingRewrite, BillingPhrase), (SecretPrototype, SecretPhrase),
                 })
        {
            var vector = For(phrase);
            await db.Projects.Where(p => p.ProjectId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Embedding, vector));
        }

        foreach (var (id, phrase) in new[] { (Acme, AcmePhrase), (Globex, "general engineering"), (HiddenCorp, HiddenCorpPhrase) })
        {
            var vector = For(phrase);
            await db.WorkExperiences.Where(w => w.WorkExperienceId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Embedding, vector));
        }

        var noteVector = For(NotePhrase);
        await db.Information.ExecuteUpdateAsync(s => s.SetProperty(i => i.Embedding, noteVector));
    }
}
