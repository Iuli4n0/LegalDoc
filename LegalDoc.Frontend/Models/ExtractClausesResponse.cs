namespace LegalDoc.Frontend.Models;

internal record ExtractClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime GeneratedAt,
    int ChunksProcessed
);
