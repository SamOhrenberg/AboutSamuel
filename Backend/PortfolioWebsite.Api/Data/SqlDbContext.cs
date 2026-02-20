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

    public DbSet<WorkExperience> WorkExperiences { get; set; }
    public DbSet<AdminToken> AdminTokens { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chat>()
            .Property(i => i.ChatId)
            .HasValueGenerator<SequentialGuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Information>()
            .Property(i => i.InformationId)
            .HasValueGenerator<SequentialGuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Keyword>()
            .Property(k => k.KeywordId)
            .HasValueGenerator<SequentialGuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Project>()
                .Property(k => k.ProjectId)
                .HasValueGenerator<SequentialGuidValueGenerator>()
                .ValueGeneratedOnAdd();

        modelBuilder.Entity<AdminToken>()
            .Property(k => k.AdminTokenId)
            .HasValueGenerator<SequentialGuidValueGenerator>()
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<WorkExperience>(entity =>
        {
            entity.HasKey(e => e.WorkExperienceId);
            entity.Property(e => e.WorkExperienceId).HasDefaultValueSql("NEWSEQUENTIALID()");
            entity.Property(e => e.Achievements).HasDefaultValue("[]");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
        });

    }
}
