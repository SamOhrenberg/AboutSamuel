using Microsoft.EntityFrameworkCore;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Data;

public class SqlDbContext : DbContext
{
    public SqlDbContext(DbContextOptions<SqlDbContext> options) : base(options) { }

    public DbSet<Information> Information { get; set; } = null!;
    public DbSet<InformationKeyword> InformationKeyword { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Information>()
            .Property(i => i.InformationId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Keyword>()
            .Property(k => k.KeywordId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<InformationKeyword>()
            .HasKey(ik => new { ik.InformationId, ik.KeywordId });

        modelBuilder.Entity<InformationKeyword>()
            .HasOne(ik => ik.Information)
            .WithMany(i => i.InformationKeywords)
            .HasForeignKey(ik => ik.InformationId);

        modelBuilder.Entity<InformationKeyword>()
            .HasOne(ik => ik.Keyword)
            .WithMany(k => k.InformationKeywords)
            .HasForeignKey(ik => ik.KeywordId);
    }
}
