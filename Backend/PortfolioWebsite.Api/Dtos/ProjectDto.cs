using System.Text.Json;
using PortfolioWebsite.Api.Data.Models;

namespace PortfolioWebsite.Api.Dtos;

public class ProjectDto
{
    public Guid ProjectId { get; set; }
    public Guid? WorkExperienceId { get; set; }

    // Derived from the linked WorkExperience — null when project is unlinked
    public string? Employer { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ImpactStatement { get; set; }
    public List<string> TechStack { get; set; } = [];
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public string? StartYear { get; set; }
    public string? EndYear { get; set; }

    public static ProjectDto FromModel(Project project)
    {
        List<string> techStack;
        try { 
            techStack = JsonSerializer.Deserialize<List<string>>(project.TechStack) ?? []; 
        }
        catch { 
            techStack = []; 
        }

        return new ProjectDto
        {
            ProjectId = project.ProjectId,
            WorkExperienceId = project.WorkExperienceId,
            Employer = project.WorkExperience?.Employer,
            Title = project.Title,
            Role = project.Role,
            Summary = project.Summary,
            Detail = project.Detail,
            ImpactStatement = project.ImpactStatement,
            TechStack = techStack,
            DisplayOrder = project.DisplayOrder,
            IsActive = project.IsActive,
            IsFeatured = project.IsFeatured,
            StartYear = project.StartYear,
            EndYear = project.EndYear
        };
    }
}