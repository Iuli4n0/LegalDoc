using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;

namespace DocumentService.Application.Commands.DeleteDocument;

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;

    public DeleteDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId);

        if (document is null)
            throw new KeyNotFoundException($"Document {request.DocumentId} not found.");

        if (document.UserId != request.UserId)
            throw new UnauthorizedAccessException("You do not have permission to delete this document.");

        // Delete from DB first; if this fails, the S3 file is still intact.
        await _documentRepository.DeleteAsync(document);

        // Delete from S3. If this fails the record is already gone, so we swallow and let the caller log.
        await _fileStorageService.DeleteFileAsync(document.S3Key);
    }
}

