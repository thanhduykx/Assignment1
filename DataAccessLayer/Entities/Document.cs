using DataAccessLayer.Enums;

namespace DataAccessLayer.Entities;

public class Document
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public DocumentFileType FileType { get; set; }
    public long FileSize { get; set; }
    public string? Chapter { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploading;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? IndexedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public User Uploader { get; set; } = null!;
    public ICollection<Chunk> Chunks { get; set; } = [];
}
