namespace LegalDoc.Frontend.Models;

public record DocumentClauseItem(
    Guid ClauseId,
    string Text,
    bool? IsAbusive,
    double? AbusiveProbability,
    DateTime? ClassifiedAt
);

