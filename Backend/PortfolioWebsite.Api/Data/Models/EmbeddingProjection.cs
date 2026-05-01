namespace PortfolioWebsite.Api.Data.Models;

public class EmbeddingProjection
{
    public Guid EmbeddingProjectionId { get; set; }

    /// <summary>GUID of the source entity (Information, Project, or WorkExperience).</summary>
    public Guid EntityId { get; set; }

    /// <summary>"information", "project", or "work"</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Display label shown on hover and in the sidebar.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Secondary label — employer name or context.</summary>
    public string? SubLabel { get; set; }

    /// <summary>Normalized X coordinate in [0, 1] range.</summary>
    public float X { get; set; }

    /// <summary>Normalized Y coordinate in [0, 1] range.</summary>
    public float Y { get; set; }

    /// <summary>JSON array of tech stack items for tooltip chips.</summary>
    public string TechStack { get; set; } = "[]";

    /// <summary>Increments each time projections are regenerated.</summary>
    public int ProjectionVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
