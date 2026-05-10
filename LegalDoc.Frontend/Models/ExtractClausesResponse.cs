namespace LegalDoc.Frontend.Models;

public record ExtractClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime GeneratedAt,
    int ChunksProcessed
);
