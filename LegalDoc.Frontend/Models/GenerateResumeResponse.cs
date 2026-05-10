namespace LegalDoc.Frontend.Models;

public record GenerateResumeResponse(
    Guid DocumentId,
    string Resume,
    DateTime GeneratedAt,
    int ChunksProcessed
);

