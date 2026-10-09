namespace PortfolioWebsite.Api.Data.Models;

/// <summary>
/// One uploaded version of Samuel's official resume PDF. Exactly one row is current. The file
/// lives in the database rather than on disk because the hosts have ephemeral filesystems.
/// The agent service seeds the first row from its bundled assets/resume.pdf when the table is
/// empty, and fills ExtractedText when the resume is analyzed.
/// </summary>
public class ResumeFile
{
    public Guid ResumeFileId { get; set; }
    public string FileName { get; set; } = string.Empty;   // as uploaded, for the history list
    public byte[] Content { get; set; } = [];
    public int SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;     // lowercase hex; also the download ETag
    public bool IsCurrent { get; set; }
    public string? ExtractedText { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
