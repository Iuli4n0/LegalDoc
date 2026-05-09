namespace LegalDoc.Frontend.Models;

internal record GetUserDocumentsResponse(
    List<GetDocumentResponse> Items,
    int TotalCount
);

