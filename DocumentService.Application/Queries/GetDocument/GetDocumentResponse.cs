using System;

namespace DocumentService.Application.Queries.GetDocument;

public record GetDocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string S3Key,
    long FileSize,
    DateTime UploadedAt,
    string? Resume,
    DateTime? ResumeGeneratedAt
);

