using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Pgvector.EntityFrameworkCore;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Data;

public class SqlDbContext : DbContext
{
    public SqlDbContext(DbContextOptions<SqlDbContext> options) : base(options) { }

    public DbSet<Information> Information { get; set; } = null!;
    public DbSet<Keyword> Keywords { get; set; } = null!;
    public DbSet<Chat> Chats { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<WorkExperience> WorkExperiences { get; set; } = null!;
    public DbSet<AdminToken> AdminTokens { get; set; } = null!;
    public DbSet<EmbeddingProjection> EmbeddingProjections { get; set; } = null!;
    public DbSet<JobFitRun> JobFitRuns { get; set; } = null!;
    public DbSet<JobFitRequirement> JobFitRequirements { get; set; } = null!;
    public DbSet<AdversarialRun> AdversarialRuns { get; set; } = null!;
    public DbSet<AdversarialCaseResult> AdversarialCaseResults { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>()
            .Property(i => i.ChatId)
            .HasValueGenerator<GuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Information>(entity =>
        {
            entity.Property(i => i.InformationId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();

            // Native pgvector column — 1536 dims for text-embedding-3-small
            entity.Property(i => i.Embedding)
                .HasColumnType("vector(1536)");
        });

        modelBuilder.Entity<Keyword>()
            .Property(k => k.KeywordId)
            .HasValueGenerator<GuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<AdminToken>()
            .Property(k => k.AdminTokenId)
            .HasValueGenerator<GuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<WorkExperience>(entity =>
        {
            entity.HasKey(e => e.WorkExperienceId);
            entity.Property(e => e.WorkExperienceId)
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Achievements).HasDefaultValue("[]");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);

            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.ProjectId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");

            entity.HasMany(p => p.WorkExperiences)
                .WithMany(w => w.Projects)
                .UsingEntity<Dictionary<string, object>>(
                    "ProjectWorkExperience",
                    j => j.HasOne<WorkExperience>()
                          .WithMany()
                          .HasForeignKey("WorkExperienceId")
                          .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Project>()
                          .WithMany()
                          .HasForeignKey("ProjectId")
                          .OnDelete(DeleteBehavior.Cascade));
        });

        modelBuilder.Entity<EmbeddingProjection>(entity =>
        {
            entity.HasKey(e => e.EmbeddingProjectionId);
            entity.Property(e => e.EmbeddingProjectionId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => e.EntityType);
        });

        modelBuilder.Entity<JobFitRun>(entity =>
        {
            entity.HasKey(e => e.JobFitRunId);
            entity.Property(e => e.JobFitRunId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();
            entity.HasIndex(e => e.ReceivedAt);

            entity.HasMany(e => e.Requirements)
                .WithOne()
                .HasForeignKey(r => r.JobFitRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobFitRequirement>(entity =>
        {
            entity.HasKey(e => e.JobFitRequirementId);
            entity.Property(e => e.JobFitRequirementId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.Citations).HasDefaultValue("[]");

            // For the gap queries: no_evidence must-haves across all runs
            entity.HasIndex(e => new { e.Status, e.Importance });
        });

        // Written by the agent service, which generates its own ids, so no value
        // generators here. Defaults matter because Python inserts with raw SQL.
        modelBuilder.Entity<AdversarialRun>(entity =>
        {
            entity.HasKey(e => e.AdversarialRunId);
            entity.HasIndex(e => e.StartedAt);
            entity.Property(e => e.Status).HasDefaultValue("running");

            entity.HasMany(e => e.Cases)
                .WithOne()
                .HasForeignKey(c => c.AdversarialRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdversarialCaseResult>(entity =>
        {
            entity.HasKey(e => e.AdversarialCaseResultId);
            entity.Property(e => e.History).HasDefaultValue("[]");
            entity.Property(e => e.ToolCalls).HasDefaultValue("[]");
            entity.Property(e => e.Claims).HasDefaultValue("[]");
            // Comparing one case across runs ("did fp-01 start passing?")
            entity.HasIndex(e => e.CaseKey);
        });

    }
}
