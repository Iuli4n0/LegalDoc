using System;

namespace DocumentService.Application.Commands.UploadDocument;

public record UploadDocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string S3Key,
    long FileSize,
    DateTime UploadedAt
);

