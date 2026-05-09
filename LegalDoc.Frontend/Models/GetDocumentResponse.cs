namespace LegalDoc.Frontend.Models;

internal record GetDocumentResponse(
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

