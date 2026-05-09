namespace LegalDoc.Frontend.Models;

internal record GenerateResumeResponse(
    Guid DocumentId,
    string Resume,
    DateTime GeneratedAt,
    int ChunksProcessed
);

