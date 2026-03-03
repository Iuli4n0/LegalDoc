using System;

namespace DocumentService.Domain.Entities;

public class Document
{
    private Document()
    {
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string S3Key { get; private set; } = null!;
    public long FileSize { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public string? Resume { get; private set; }
    public DateTime? ResumeGeneratedAt { get; private set; }

    public static Document Create(string fileName, string contentType, string s3Key, long fileSize, string userId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        
        if (string.IsNullOrWhiteSpace(s3Key))
            throw new ArgumentException("S3 key cannot be empty.", nameof(s3Key));
        
        if (fileSize <= 0)
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSize));
        
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        
        return new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            ContentType = contentType,
            S3Key = s3Key,
            FileSize = fileSize,
            UploadedAt = DateTime.UtcNow
        };
    }

    public void SetResume(string resume)
    {
        if (string.IsNullOrWhiteSpace(resume))
            throw new ArgumentException("Resume cannot be empty.", nameof(resume));
        
        Resume = resume;
        ResumeGeneratedAt = DateTime.UtcNow;
    }
}