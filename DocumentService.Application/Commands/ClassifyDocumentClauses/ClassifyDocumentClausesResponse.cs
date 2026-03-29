using System;
using System.Collections.Generic;

namespace DocumentService.Application.Commands.ClassifyDocumentClauses;

public record ClassifyDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<ClassifiedClauseResponseItem> Clauses,
    DateTime ClassifiedAt
);

public record ClassifiedClauseResponseItem(
    Guid ClauseId,
    string Text,
    bool IsAbusive,
    double AbusiveProbability,
    DateTime ClassifiedAt
);

