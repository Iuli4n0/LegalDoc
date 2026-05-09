namespace LegalDoc.Frontend.Models;

internal record DocumentClauseItem(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);

