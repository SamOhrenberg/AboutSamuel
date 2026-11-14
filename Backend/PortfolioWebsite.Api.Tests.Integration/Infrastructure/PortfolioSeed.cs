using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioWebsite.Api.Data;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Tests.Integration.Infrastructure;

/// <summary>
/// A small known portfolio, written as the database owner. Tests in the Api collection run
/// one at a time, so a test class can call ResetAsync in its InitializeAsync.
///
/// Visible: Acme (current), Globex; projects Portfolio Chat (featured, Acme),
/// Portfolio Chat again (Acme, so slug portfolio-chat-2), Billing Rewrite (Globex and the
/// hidden employer). Hidden: Hidden Corp and Secret Prototype. Globex's Achievements is
/// malformed JSON on purpose.
/// </summary>
public static class PortfolioSeed
{
    public static readonly Guid Acme = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Globex = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid HiddenCorp = Guid.Parse("a0000000-0000-0000-0000-000000000003");

    public static readonly Guid PortfolioChat = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid BillingRewrite = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    public static readonly Guid PortfolioChatTwo = Guid.Parse("b0000000-0000-0000-0000-000000000003");
    public static readonly Guid SecretPrototype = Guid.Parse("b0000000-0000-0000-0000-000000000004");

    public static async Task ResetAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE "ProjectWorkExperience", "Projects", "WorkExperiences", "Information" CASCADE""");

        var acme = new WorkExperience
        {
            WorkExperienceId = Acme, Employer = "Acme", Title = "Senior Engineer", StartYear = "2021",
            EndYear = null, Summary = "Builds AI features.", Achievements = """["Cut deploy time 60%", "Led 4 engineers"]""",
            DisplayOrder = 0,
        };
        var globex = new WorkExperience
        {
            WorkExperienceId = Globex, Employer = "Globex", Title = "Engineer", StartYear = "2017",
            EndYear = "2020", Achievements = "not json", DisplayOrder = 1,
        };
        var hidden = new WorkExperience
        {
            WorkExperienceId = HiddenCorp, Employer = "Hidden Corp", Title = "Intern", StartYear = "2015",
            EndYear = "2016", DisplayOrder = 2, IsActive = false,
        };

        db.WorkExperiences.AddRange(acme, globex, hidden);
        db.Projects.AddRange(
            new Project
            {
                ProjectId = PortfolioChat, Title = "Portfolio Chat", Role = "Lead", Summary = "RAG chat about my work.",
                Detail = "Agent service plus pgvector.", ImpactStatement = "Answers recruiter questions.",
                TechStack = """["C#", "PostgreSQL", "Azure OpenAI"]""", StartYear = "2024", EndYear = null,
                DisplayOrder = 0, IsFeatured = true, WorkExperiences = [acme],
            },
            new Project
            {
                ProjectId = BillingRewrite, Title = "Billing Rewrite", Role = "Developer", Summary = "Rewrote billing.",
                TechStack = """["c#", "SQL Server"]""", StartYear = "2018", EndYear = "2020",
                DisplayOrder = 1, WorkExperiences = [globex, hidden],
            },
            new Project
            {
                ProjectId = PortfolioChatTwo, Title = "Portfolio Chat", Role = "Developer", Summary = "The first version.",
                TechStack = """["python", "postgresql"]""", StartYear = "2022", EndYear = "2023",
                DisplayOrder = 2, WorkExperiences = [acme],
            },
            new Project
            {
                ProjectId = SecretPrototype, Title = "Secret Prototype", Role = "Developer", Summary = "Not public.",
                TechStack = """["Rust"]""", StartYear = "2025", DisplayOrder = 3, IsActive = false,
                WorkExperiences = [acme],
            });
        db.Information.Add(new Information { Text = "Samuel likes building developer tools." });

        await db.SaveChangesAsync();
    }
}
