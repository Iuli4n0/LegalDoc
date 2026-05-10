namespace LegalDoc.Frontend.Models;

public record GetUserDocumentsResponse(
    List<GetDocumentResponse> Items,
    int TotalCount
);

