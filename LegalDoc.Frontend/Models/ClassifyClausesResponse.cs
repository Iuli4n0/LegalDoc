namespace LegalDoc.Frontend.Models;

internal record ClassifyClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime ClassifiedAt
);

