namespace LegalDoc.Frontend.Models;

public record UploadDocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string S3Key,
    long FileSize,
    DateTime UploadedAt
);

