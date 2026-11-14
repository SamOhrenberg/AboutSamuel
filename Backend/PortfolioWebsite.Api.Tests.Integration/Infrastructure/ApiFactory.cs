using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
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

    public const string ReadOnlyRole = "mcp_readonly";
    private const string ReadOnlyPassword = "integration-tests-only-mcp-password";

    /// <summary>The database owner. Use it to seed data.</summary>
    public string ConnectionString => _db.GetConnectionString();

    /// <summary>The least-privilege login from infra/sql/mcp-readonly-role.sql, which the
    /// app's PublicReadOnly connection also uses.</summary>
    public string ReadOnlyConnectionString => new NpgsqlConnectionStringBuilder(ConnectionString)
    {
        Username = ReadOnlyRole,
        Password = ReadOnlyPassword,
    }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        // No migration creates the extension (prod got it by hand), so a fresh database
        // fails at AddVectors without this.
        var result = await _db.ExecScriptAsync("CREATE EXTENSION IF NOT EXISTS vector;");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Creating the vector extension failed: {result.Stderr}");

        // Boot the host now: startup migrations create the tables the role script grants on.
        // Nothing touches the PublicReadOnly connection during startup, so the role not
        // existing yet is fine.
        _ = Server;
        await ApplyReadOnlyRoleScriptAsync();
    }

    /// <summary>Runs the committed role script the way the README says to: psql, with the
    /// password passed as a variable.</summary>
    public async Task ApplyReadOnlyRoleScriptAsync()
    {
        const string containerPath = "/tmp/mcp-readonly-role.sql";
        var script = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "mcp-readonly-role.sql"));
        await _db.CopyAsync(script, containerPath);

        var owner = new NpgsqlConnectionStringBuilder(ConnectionString);
        var result = await _db.ExecAsync([
            "psql", "-v", "ON_ERROR_STOP=1", "-v", $"mcp_password={ReadOnlyPassword}",
            "--username", owner.Username!, "--dbname", owner.Database!, "--file", containerPath,
        ]);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"The read-only role script failed: {result.Stderr}");
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
        builder.UseSetting("ConnectionStrings:PublicReadOnly", ReadOnlyConnectionString);
        builder.UseSetting("AdminSettings:JwtSecret", "integration-tests-only-secret-0123456789abcdef");
        builder.UseSetting("AzureOpenAI:Endpoint", "https://azure-openai.invalid/");
        builder.UseSetting("AgentService:Url", "http://agent-service.invalid/");
        // Every test request shares one client IP, so the per-IP limit would leak between
        // tests. Its behaviour is covered by the unit tests.
        builder.UseSetting("Mcp:SemanticSearch:PerIpPerHour", "100000");
        builder.UseSetting("Mcp:SemanticSearch:DailyCap", "100000");
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
