using System;

namespace DocumentService.Application.Commands.AddDocumentClause;

public record AddDocumentClauseResponse(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);
