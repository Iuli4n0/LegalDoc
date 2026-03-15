using System;
using System.Collections.Generic;

namespace DocumentService.Application.Queries.GetDocumentClauses;

public record GetDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<string> Clauses,
    DateTime? GeneratedAt
);
