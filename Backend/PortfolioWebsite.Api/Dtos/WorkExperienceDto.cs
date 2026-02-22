using System.Text.Json;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Dtos;

public class WorkExperienceDto
{
    public Guid WorkExperienceId { get; set; }
    public string Employer { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }
    public string? Summary { get; set; }
    public List<string> Achievements { get; set; } = [];
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public bool HasEmbedding { get; set; }

    public static WorkExperienceDto FromModel(WorkExperience job)
    {
        List<string> achievements;
        try
        {
            achievements = JsonSerializer.Deserialize<List<string>>(job.Achievements) ?? [];
        }
        catch
        {
            achievements = [];
        }

        return new WorkExperienceDto
        {
            WorkExperienceId = job.WorkExperienceId,
            Employer        = job.Employer,
            Title           = job.Title,
            StartYear       = job.StartYear,
            EndYear         = job.EndYear,
            Summary         = job.Summary,
            Achievements    = achievements,
            DisplayOrder    = job.DisplayOrder,
            IsActive        = job.IsActive,
            HasEmbedding    = job.EmbeddingJson != null
        };
    }
}
