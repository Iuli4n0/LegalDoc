using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;

namespace DocumentService.Application.Queries.DownloadDocument;

public class DownloadDocumentQueryHandler : IRequestHandler<DownloadDocumentQuery, DownloadDocumentResult>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;

    public DownloadDocumentQueryHandler(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<DownloadDocumentResult> Handle(DownloadDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.Id);

        if (document is null)
            throw new KeyNotFoundException($"Document {request.Id} not found.");

        if (document.UserId != request.UserId)
            throw new UnauthorizedAccessException("You do not have permission to download this document.");

        var stream = await _fileStorageService.DownloadFileAsync(document.S3Key);

        return new DownloadDocumentResult(stream, document.ContentType, document.FileName);
    }
}

