using PortfolioWebsite.Api.Mcp.Contracts;

namespace PortfolioWebsite.Api.Mcp;

/// <summary>One project's input to the skill map.</summary>
public sealed record SkillSource(
    ProjectRef Project,
    IReadOnlyList<string> TechStack,
    YearRange Years,
    IReadOnlyList<ExperienceRef> Experiences);

/// <summary>
/// Builds the skill list from Project.TechStack (there's no skills table). Names group
/// case-insensitively and show the most common spelling.
/// </summary>
public static class SkillAggregator
{
    public static IReadOnlyList<SkillSummary> Aggregate(IEnumerable<SkillSource> projects, int minProjects = 1)
    {
        minProjects = Math.Max(1, minProjects);
        var skills = new Dictionary<string, Accumulator>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var project in projects)
        {
            // A project listing "SQL" twice still counts once.
            foreach (var tech in project.TechStack.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!skills.TryGetValue(tech, out var skill))
                    skills[tech] = skill = new Accumulator(order++);
                skill.Add(tech, project);
            }

            // Spelling votes count every mention, including repeats within a project.
            foreach (var tech in project.TechStack)
                skills[tech].Vote(tech);
        }

        return skills.Values
            .Where(s => s.Projects.Count >= minProjects)
            .Select(s => s.ToSummary())
            .OrderByDescending(s => s.ProjectCount)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class Accumulator(int firstSeen)
    {
        private readonly Dictionary<string, (int Count, int FirstSeen)> _spellings = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, ExperienceRef> _employers = [];
        private int? _firstYear;
        private int? _lastYear;
        private bool _isCurrent;

        public int FirstSeen { get; } = firstSeen;
        public List<ProjectRef> Projects { get; } = [];

        public void Add(string tech, SkillSource source)
        {
            Projects.Add(source.Project);
            foreach (var experience in source.Experiences)
                _employers.TryAdd(experience.Id, experience);

            if (source.Years.Start is { } start)
                _firstYear = _firstYear is { } f ? Math.Min(f, start) : start;
            if (source.Years.End is { } end)
                _lastYear = _lastYear is { } l ? Math.Max(l, end) : end;
            _isCurrent |= source.Years.IsCurrent;
        }

        public void Vote(string spelling)
        {
            _spellings[spelling] = _spellings.TryGetValue(spelling, out var s)
                ? (s.Count + 1, s.FirstSeen)
                : (1, _spellings.Count);
        }

        public SkillSummary ToSummary()
        {
            var name = _spellings
                .OrderByDescending(s => s.Value.Count)
                .ThenBy(s => s.Value.FirstSeen)
                .First().Key;

            return new SkillSummary(name, Projects.Count, _firstYear, _lastYear, _isCurrent,
                Projects, _employers.Values.ToList());
        }
    }
}
