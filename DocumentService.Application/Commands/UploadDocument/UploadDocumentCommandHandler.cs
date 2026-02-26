using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using MediatR;

namespace DocumentService.Application.Commands.UploadDocument;

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, UploadDocumentResponse>
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IDocumentRepository _documentRepository;

    public UploadDocumentCommandHandler(
        IFileStorageService fileStorageService,
        IDocumentRepository documentRepository)
    {
        _fileStorageService = fileStorageService;
        _documentRepository = documentRepository;
    }

    public async Task<UploadDocumentResponse> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var s3Key = await _fileStorageService.UploadFileAsync(
            request.FileStream, 
            request.FileName, 
            request.ContentType);

        var document = Document.Create(
            request.FileName, 
            request.ContentType, 
            s3Key, 
            request.FileSize);

        await _documentRepository.AddAsync(document);

        return new UploadDocumentResponse(
            document.Id,
            document.FileName,
            document.ContentType,
            document.S3Key,
            document.FileSize,
            document.UploadedAt
        );
    }
}

