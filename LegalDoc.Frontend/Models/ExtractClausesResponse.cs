namespace LegalDoc.Frontend.Models;

public record ExtractClausesResponse(
    Guid DocumentId,
    IReadOnlyList<string> Clauses,
    DateTime GeneratedAt,
    int ChunksProcessed
);

