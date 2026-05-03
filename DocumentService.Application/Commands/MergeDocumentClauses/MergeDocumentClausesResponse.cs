using System;

namespace DocumentService.Application.Commands.MergeDocumentClauses;

public record MergeDocumentClausesResponse(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);
