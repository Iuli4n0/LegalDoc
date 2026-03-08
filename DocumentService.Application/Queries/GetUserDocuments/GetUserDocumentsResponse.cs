using System;
using System.Collections.Generic;
using DocumentService.Application.Queries.GetDocument;

namespace DocumentService.Application.Queries.GetUserDocuments;

public record GetUserDocumentsResponse(
    IEnumerable<GetDocumentResponse> Items,
    int TotalCount
);

