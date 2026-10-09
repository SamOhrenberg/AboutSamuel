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
    public DbSet<RecruiterTriageSettings> RecruiterTriageSettings { get; set; } = null!;
    public DbSet<RecruiterPosting> RecruiterPostings { get; set; } = null!;
    public DbSet<RecruiterEmail> RecruiterEmails { get; set; } = null!;
    public DbSet<RecruiterPitch> RecruiterPitches { get; set; } = null!;
    public DbSet<ResumeFile> ResumeFiles { get; set; } = null!;
    public DbSet<ResumeAnalysis> ResumeAnalyses { get; set; } = null!;
    public DbSet<ResumeSuggestion> ResumeSuggestions { get; set; } = null!;

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

        // Recruiter triage. Written by the agent service (which makes its own ids), read and
        // edited by the admin panel. Defaults matter because Python inserts with raw SQL.
        modelBuilder.Entity<RecruiterTriageSettings>(entity =>
        {
            entity.HasKey(e => e.RecruiterTriageSettingsId);
            entity.Property(e => e.RecruiterTriageSettingsId).ValueGeneratedNever();
            entity.Property(e => e.ShadowMode).HasDefaultValue(true);
            entity.Property(e => e.HomeState).HasDefaultValue("OK");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            // The single row, seeded with the preferences agreed on 2026-10-06
            entity.HasData(new RecruiterTriageSettings
            {
                RecruiterTriageSettingsId = 1,
                TriageEnabled = false,
                ShadowMode = true,
                RemotePayFloor = 90_000,
                HybridPayFloor = 90_000,
                OnsitePayFloor = 90_000,
                HoursPerYear = 2080,
                AllowRemote = true,
                AllowHybrid = true,
                AllowOnsite = true,
                AcceptableLocations = "[\"Oklahoma City\",\"OKC\",\"Edmond\",\"Moore\",\"Midwest City\",\"Del City\","
                    + "\"Yukon\",\"Mustang\",\"Bethany\",\"Warr Acres\",\"Nichols Hills\",\"The Village\",\"Choctaw\","
                    + "\"Norman\",\"Tinker AFB\",\"Tinker Air Force Base\",\"Piedmont\",\"Newcastle\",\"Spencer\","
                    + "\"Harrah\",\"Jones\"]",
                HomeState = "OK",
                AllowedEmploymentTypes = "[\"full_time\",\"contract_to_hire\"]",
                DealbreakerContractTerms = "[\"c2c\",\"1099\"]",
                FreeTextRequirements = "The role must be primarily hands-on software development or AI engineering, "
                    + "where I'm writing code. Not desktop support, help desk, manual QA, project management, "
                    + "security analysis, or BI reporting without real development.",
                UpdatedAt = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            });
        });

        modelBuilder.Entity<RecruiterPosting>(entity =>
        {
            entity.HasKey(e => e.RecruiterPostingId);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.ReviewStatus).HasDefaultValue("pending");
            entity.Property(e => e.Role).HasDefaultValue("{}");
            entity.Property(e => e.Reasons).HasDefaultValue("[]");
            entity.Property(e => e.Missing).HasDefaultValue("[]");
            entity.Property(e => e.Conflicts).HasDefaultValue("[]");
            entity.HasIndex(e => new { e.ReviewStatus, e.LastSeenAt });  // the review queue
            entity.HasIndex(e => e.LastSeenAt);                          // matching recent postings

            entity.HasMany(e => e.Pitches)
                .WithOne(p => p.Posting)
                .HasForeignKey(p => p.RecruiterPostingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecruiterEmail>(entity =>
        {
            entity.HasKey(e => e.RecruiterEmailId);
            entity.HasIndex(e => e.GmailMessageId).IsUnique();  // never process a message twice
            entity.HasIndex(e => e.GmailThreadId);
            entity.Property(e => e.Action).HasDefaultValue("none");

            entity.HasMany(e => e.Pitches)
                .WithOne(p => p.Email)
                .HasForeignKey(p => p.RecruiterEmailId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecruiterPitch>(entity =>
        {
            entity.HasKey(e => e.RecruiterPitchId);
            entity.Property(e => e.Role).HasDefaultValue("{}");
        });

        // Resume versions. The agent service inserts the seed row with raw SQL, so the
        // database supplies the defaults.
        modelBuilder.Entity<ResumeFile>(entity =>
        {
            entity.HasKey(e => e.ResumeFileId);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Sha256);
            // At most one current resume
            entity.HasIndex(e => e.IsCurrent).IsUnique().HasFilter("\"IsCurrent\"");
        });

        // Resume analysis. Written by the agent service with raw SQL, so defaults live here.
        modelBuilder.Entity<ResumeAnalysis>(entity =>
        {
            entity.HasKey(e => e.ResumeAnalysisId);
            entity.Property(e => e.Status).HasDefaultValue("running");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StartedAt);

            entity.HasOne(e => e.ResumeFile)
                .WithMany()
                .HasForeignKey(e => e.ResumeFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Suggestions)
                .WithOne(s => s.Analysis)
                .HasForeignKey(s => s.ResumeAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResumeSuggestion>(entity =>
        {
            entity.HasKey(e => e.ResumeSuggestionId);
            entity.Property(e => e.Changes).HasDefaultValue("{}");
            entity.Property(e => e.Status).HasDefaultValue("pending");
            entity.HasIndex(e => new { e.Status, e.ResumeAnalysisId });  // the review queue
        });

    }
}
