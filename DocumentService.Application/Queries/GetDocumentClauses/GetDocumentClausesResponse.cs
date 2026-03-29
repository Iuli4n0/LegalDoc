using System;
using System.Collections.Generic;

namespace DocumentService.Application.Queries.GetDocumentClauses;

public record GetDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<GetDocumentClauseResponseItem> Clauses,
    DateTime? GeneratedAt
);

public record GetDocumentClauseResponseItem(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);
