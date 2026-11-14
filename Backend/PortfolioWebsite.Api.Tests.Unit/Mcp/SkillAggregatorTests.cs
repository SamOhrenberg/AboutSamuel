using PortfolioWebsite.Api.Mcp;
using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class SkillAggregatorTests
{
    private const int Now = 2026;

    private static readonly ExperienceRef Acme = new(Guid.NewGuid(), "Acme", "Engineer", "https://x/acme");
    private static readonly ExperienceRef Globex = new(Guid.NewGuid(), "Globex", "Lead", "https://x/globex");

    private static SkillSource Project(string slug, string? start, string? end, string[] tech, params ExperienceRef[] experiences) =>
        new(new ProjectRef(slug, Guid.NewGuid(), slug, start, end, $"https://x/{slug}"),
            tech, YearRange.From(start, end, Now), experiences);

    [Fact]
    public void Groups_case_insensitively_and_counts_projects_once()
    {
        var skills = SkillAggregator.Aggregate([
            Project("a", "2019", "2020", ["C#", "c#", "SQL"], Acme),
            Project("b", "2021", "2022", ["c#"], Globex),
            Project("c", "2023", "2023", ["C#"]),
        ]);

        var csharp = Assert.Single(skills, s => s.Name.Equals("c#", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, csharp.ProjectCount);
        Assert.Equal(["a", "b", "c"], csharp.Projects.Select(p => p.Slug));
        Assert.Equal("C#", csharp.Name);
    }

    [Fact]
    public void Tracks_year_span_and_current_use()
    {
        var skills = SkillAggregator.Aggregate([
            Project("a", "2017", "2019", ["Python"]),
            Project("b", "2024", null, ["Python"]),
            Project("c", "2015", "2016", ["Perl"]),
        ]);

        var python = skills.Single(s => s.Name == "Python");
        Assert.Equal(2017, python.FirstYear);
        Assert.Equal(Now, python.LastYear);
        Assert.True(python.IsCurrent);

        var perl = skills.Single(s => s.Name == "Perl");
        Assert.Equal(2015, perl.FirstYear);
        Assert.Equal(2016, perl.LastYear);
        Assert.False(perl.IsCurrent);
    }

    [Fact]
    public void Missing_years_leave_the_span_null()
    {
        var skill = Assert.Single(SkillAggregator.Aggregate([Project("a", null, "TBD", ["Rust"])]));

        Assert.Null(skill.FirstYear);
        Assert.Null(skill.LastYear);
        Assert.False(skill.IsCurrent);
    }

    [Fact]
    public void Collects_distinct_employers_in_first_seen_order()
    {
        var skill = Assert.Single(SkillAggregator.Aggregate([
            Project("a", "2020", "2021", ["Go"], Globex),
            Project("b", "2021", "2022", ["Go"], Acme, Globex),
        ]));

        Assert.Equal(["Globex", "Acme"], skill.Employers.Select(e => e.Employer));
    }

    [Fact]
    public void Sorts_by_project_count_then_name_and_applies_min_projects()
    {
        var projects = new[]
        {
            Project("a", "2020", "2020", ["zig", "Azure", "bash"]),
            Project("b", "2020", "2020", ["Azure", "Bash"]),
        };

        Assert.Equal(["Azure", "bash", "zig"], SkillAggregator.Aggregate(projects).Select(s => s.Name));
        Assert.Equal(["Azure", "bash"], SkillAggregator.Aggregate(projects, minProjects: 2).Select(s => s.Name));
        Assert.Equal(3, SkillAggregator.Aggregate(projects, minProjects: -5).Count);
    }

    [Fact]
    public void Most_common_spelling_wins_with_ties_going_to_the_first_seen()
    {
        var skills = SkillAggregator.Aggregate([
            Project("a", "2020", "2020", ["postgres", "TypeScript"]),
            Project("b", "2020", "2020", ["Postgres", "typescript"]),
            Project("c", "2020", "2020", ["Postgres"]),
        ]);

        Assert.Contains(skills, s => s.Name == "Postgres");
        Assert.Contains(skills, s => s.Name == "TypeScript");
    }

    [Fact]
    public void No_projects_means_no_skills() =>
        Assert.Empty(SkillAggregator.Aggregate([]));
}
