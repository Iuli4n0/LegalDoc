using MediatR;

namespace DocumentService.Application.Queries.GetUserDocuments;

public record GetUserDocumentsQuery(
    string UserId,
    int Page,
    int PageSize,
    string SortBy,
    bool Ascending
) : IRequest<GetUserDocumentsResponse>;

