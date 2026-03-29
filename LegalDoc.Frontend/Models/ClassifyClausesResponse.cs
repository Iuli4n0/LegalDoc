namespace LegalDoc.Frontend.Models;

public record ClassifyClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime ClassifiedAt
);

