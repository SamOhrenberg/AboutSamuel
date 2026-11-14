using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>
/// Read-only view of the public portfolio tables for the MCP server. It maps only Projects,
/// WorkExperiences, their join table and Information, so private tables (chats, recruiter
/// data, resume analyses, tokens) aren't reachable through it at all. SqlDbContext owns the
/// schema: every table here is excluded from migrations, and saving always throws. In prod it
/// connects as mcp_readonly (infra/sql/mcp-readonly-role.sql), which can't write either.
/// </summary>
public sealed class PublicPortfolioDbContext(DbContextOptions<PublicPortfolioDbContext> options)
    : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Information> Information => Set<Information>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasMany(p => p.WorkExperiences)
                .WithMany(w => w.Projects)
                .UsingEntity<Dictionary<string, object>>(
                    "ProjectWorkExperience",
                    j => j.HasOne<WorkExperience>().WithMany().HasForeignKey("WorkExperienceId"),
                    j => j.HasOne<Project>().WithMany().HasForeignKey("ProjectId"),
                    j => j.ToTable("ProjectWorkExperience", t => t.ExcludeFromMigrations()));
        });

        modelBuilder.Entity<WorkExperience>(entity =>
        {
            entity.ToTable("WorkExperiences", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.WorkExperienceId);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
        });

        modelBuilder.Entity<Information>(entity =>
        {
            entity.ToTable("Information", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.InformationId);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            // Keywords is a private table; without this EF would discover it by convention.
            entity.Ignore(e => e.Keywords);
        });
    }

    private const string ReadOnlyMessage = "PublicPortfolioDbContext is read-only.";

    public override int SaveChanges() => throw new InvalidOperationException(ReadOnlyMessage);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException(ReadOnlyMessage);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(ReadOnlyMessage);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(ReadOnlyMessage);
}
