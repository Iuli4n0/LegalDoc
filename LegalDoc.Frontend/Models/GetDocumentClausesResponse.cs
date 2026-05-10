using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

public record GetDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime? GeneratedAt
);
