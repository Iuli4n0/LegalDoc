using System;
using System.Collections.Generic;

namespace DocumentService.Application.Commands.GenerateDocumentClauses;

public record GenerateDocumentClausesResponse(
    Guid DocumentId,
    IReadOnlyList<string> Clauses,
    DateTime GeneratedAt,
    int ChunksProcessed
);

