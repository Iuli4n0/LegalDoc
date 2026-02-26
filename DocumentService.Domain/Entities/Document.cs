using System;

namespace DocumentService.Domain.Entities;

public class Document
{
    private Document()
    {
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string S3Key { get; private set; } = null!;
    public long FileSize { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public string? Resume { get; private set; }
    public DateTime? ResumeGeneratedAt { get; private set; }

    public static Document Create(string fileName, string contentType, string s3Key, long fileSize)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            S3Key = s3Key,
            FileSize = fileSize,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void SetResume(string resume)
    {
        Resume = resume;
        ResumeGeneratedAt = DateTime.UtcNow;
    }
}