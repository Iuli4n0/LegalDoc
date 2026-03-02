namespace LegalDoc.Frontend.Models;

public record GetDocumentResponse(
    Guid Id,
    string UserId,
    string FileName,
    string ContentType,
    string S3Key,
    long FileSize,
    DateTime UploadedAt,
    string? Resume,
    DateTime? ResumeGeneratedAt
);

