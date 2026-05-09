using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

internal record GetDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<DocumentClauseItem> Clauses,
    DateTime? GeneratedAt
);
