using System;

namespace DocumentService.Application.Commands.GenerateDocumentResume;

public record GenerateDocumentResumeResponse(
    Guid DocumentId,
    string Resume,
    DateTime GeneratedAt,
    int ChunksProcessed
);

