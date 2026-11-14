using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortfolioWebsite.Api.Services;
using Testcontainers.PostgreSql;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway pgvector Postgres. Program.Main runs as in prod,
/// so startup migrations apply to the container. Settings go in through UseSetting because
/// Program reads JwtSecret and the connection string before Build(), and UseSetting values
/// arrive as command-line args: visible that early and above appsettings.json and env vars.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public string ConnectionString => _db.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        // No migration creates the extension (prod got it by hand), so a fresh database
        // fails at AddVectors without this.
        var result = await _db.ExecScriptAsync("CREATE EXTENSION IF NOT EXISTS vector;");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Creating the vector extension failed: {result.Stderr}");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("AdminSettings:JwtSecret", "integration-tests-only-secret-0123456789abcdef");
        builder.UseSetting("AzureOpenAI:Endpoint", "https://azure-openai.invalid/");
        builder.UseSetting("AgentService:Url", "http://agent-service.invalid/");
        // Keep telemetry off even if the machine running the tests has this env var set.
        builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", "");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
