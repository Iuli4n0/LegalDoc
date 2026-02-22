using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Interfaces;
using MediatR;

namespace DocumentService.Application.Queries.GetDocument;

public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, GetDocumentResponse?>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<GetDocumentResponse?> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.Id);
        
        if (document is null)
            return null;

        return new GetDocumentResponse(
            document.Id,
            document.FileName,
            document.ContentType,
            document.S3Key,
            document.FileSize,
            document.UploadedAt
        );
    }
}

