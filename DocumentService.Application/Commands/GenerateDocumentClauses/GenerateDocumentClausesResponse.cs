using System;
using System.Collections.Generic;

namespace DocumentService.Application.Commands.GenerateDocumentClauses;

public record GenerateDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<GenerateDocumentClauseResponseItem> Clauses,
    DateTime GeneratedAt,
    int ChunksProcessed
);

public record GenerateDocumentClauseResponseItem(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);
