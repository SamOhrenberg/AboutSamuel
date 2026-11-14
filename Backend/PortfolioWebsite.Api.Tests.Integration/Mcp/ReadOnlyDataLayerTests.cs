using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PortfolioWebsite.Api.Data.Models;
using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;

namespace PortfolioWebsite.Api.Tests.Integration.Mcp;

/// <summary>
/// The layers that keep the public MCP data path from writing: the context refuses to save,
/// the role has only SELECT on four tables, and its sessions default to read-only.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReadOnlyDataLayerTests(ApiFactory factory) : IAsyncLifetime
{
    private const string InsufficientPrivilege = "42501";
    private const string ReadOnlyTransaction = "25006";

    public Task InitializeAsync() => PortfolioSeed.ResetAsync(factory);
    public Task DisposeAsync() => Task.CompletedTask;

    private PublicPortfolioDbContext CreateContext() =>
        factory.Services.GetRequiredService<IDbContextFactory<PublicPortfolioDbContext>>().CreateDbContext();

    [Fact]
    public async Task Context_connects_as_the_read_only_role()
    {
        await using var db = CreateContext();

        var user = await db.Database.SqlQuery<string>($"SELECT current_user::text AS \"Value\"").SingleAsync();

        Assert.Equal(ApiFactory.ReadOnlyRole, user);
    }

    [Fact]
    public async Task Context_reads_every_mapped_table()
    {
        await using var db = CreateContext();

        Assert.Equal(4, await db.Projects.CountAsync());
        Assert.Equal(3, await db.WorkExperiences.CountAsync());
        Assert.Equal(1, await db.Information.CountAsync());
        Assert.Equal(2, (await db.Projects.Include(p => p.WorkExperiences)
            .SingleAsync(p => p.ProjectId == PortfolioSeed.BillingRewrite)).WorkExperiences.Count);
    }

    [Fact]
    public async Task Context_does_not_track_queries()
    {
        await using var db = CreateContext();

        await db.Projects.ToListAsync();

        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Context_refuses_to_save()
    {
        await using var db = CreateContext();
        db.Projects.Add(new Project { Title = "Injected" });

        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges(acceptAllChangesOnSuccess: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(acceptAllChangesOnSuccess: true));
    }

    [Fact]
    public void Context_maps_only_the_public_tables()
    {
        using var db = CreateContext();

        var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Order(StringComparer.Ordinal);

        Assert.Equal(["Information", "ProjectWorkExperience", "Projects", "WorkExperiences"], tables);
    }

    public static TheoryData<string> Writes => new()
    {
        """INSERT INTO "Projects" ("ProjectId", "Title", "Role", "Summary", "TechStack", "DisplayOrder", "IsFeatured", "IsActive") VALUES (gen_random_uuid(), 'x', 'x', 'x', '[]', 0, false, true)""",
        """UPDATE "Projects" SET "Title" = 'x'""",
        """DELETE FROM "Projects" """,
        """INSERT INTO "Information" ("InformationId", "Text") VALUES (gen_random_uuid(), 'x')""",
        """UPDATE "WorkExperiences" SET "IsActive" = false""",
        """DELETE FROM "ProjectWorkExperience" """,
        """TRUNCATE "Information" """,
        """CREATE TABLE "Evil" ("Id" int)""",
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Role_sessions_default_to_read_only(string sql)
    {
        var error = await ExecuteAsReadOnlyRoleAsync(sql, allowWrites: false);

        Assert.Equal(ReadOnlyTransaction, error);
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Role_lacks_write_privileges_even_with_read_only_off(string sql)
    {
        var error = await ExecuteAsReadOnlyRoleAsync(sql, allowWrites: true);

        Assert.Equal(InsufficientPrivilege, error);
    }

    [Theory]
    [InlineData("Chats")]
    [InlineData("Keywords")]
    [InlineData("AdminTokens")]
    [InlineData("RecruiterEmails")]
    [InlineData("ResumeFiles")]
    [InlineData("JobFitRuns")]
    public async Task Role_cannot_read_private_tables(string table)
    {
        var error = await ExecuteAsReadOnlyRoleAsync($"""SELECT count(*) FROM "{table}" """, allowWrites: false);

        Assert.Equal(InsufficientPrivilege, error);
    }

    [Fact]
    public async Task Role_script_is_safe_to_rerun()
    {
        await factory.ApplyReadOnlyRoleScriptAsync();

        await using var db = CreateContext();
        Assert.Equal(4, await db.Projects.CountAsync());
    }

    /// <summary>Runs one statement as mcp_readonly and returns its SQLSTATE, or null if it
    /// succeeded. Changes are rolled back either way.</summary>
    private async Task<string?> ExecuteAsReadOnlyRoleAsync(string sql, bool allowWrites)
    {
        await using var connection = new NpgsqlConnection(factory.ReadOnlyConnectionString);
        await connection.OpenAsync();

        if (allowWrites)
        {
            await using var off = new NpgsqlCommand("SET default_transaction_read_only = off", connection);
            await off.ExecuteNonQueryAsync();
        }

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
