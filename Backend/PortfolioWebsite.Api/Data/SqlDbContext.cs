using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>()
            .Property(i => i.ChatId)
            .HasValueGenerator<GuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Information>()
            .Property(i => i.InformationId)
            .HasValueGenerator<GuidValueGenerator>()
            .ValueGeneratedOnAdd();

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
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.ProjectId)
                .HasValueGenerator<GuidValueGenerator>()
                .ValueGeneratedOnAdd();

            // Many-to-many with explicit join table and clean column names
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
    }
}