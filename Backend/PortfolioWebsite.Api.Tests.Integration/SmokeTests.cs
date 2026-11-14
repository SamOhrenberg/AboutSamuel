using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration;

[Collection(ApiCollection.Name)]
public class SmokeTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_applies_all_migrations()
    {
        // CreateClient boots the host if no other test has yet; Main migrates during startup.
        factory.CreateClient().Dispose();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlDbContext>();

        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
