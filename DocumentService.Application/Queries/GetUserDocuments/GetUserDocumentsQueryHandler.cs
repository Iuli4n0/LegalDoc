using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.GetDocument;
using MediatR;

namespace DocumentService.Application.Queries.GetUserDocuments;

public class GetUserDocumentsQueryHandler : IRequestHandler<GetUserDocumentsQuery, GetUserDocumentsResponse>
{
    private readonly IDocumentRepository _documentRepository;

    public GetUserDocumentsQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<GetUserDocumentsResponse> Handle(GetUserDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetByUserIdAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.Ascending);

        var totalCount = await _documentRepository.CountByUserIdAsync(request.UserId);

        var items = documents.Select(d => new GetDocumentResponse(
            d.Id,
            d.UserId,
            d.FileName,
            d.ContentType,
            d.S3Key,
            d.FileSize,
            d.UploadedAt,
            d.Resume,
            d.ResumeGeneratedAt));

        return new GetUserDocumentsResponse(items, totalCount);
    }
}

