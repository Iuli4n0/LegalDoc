using System.IO;
using MediatR;

namespace DocumentService.Application.Commands.UploadDocument;

public class UploadDocumentCommand : IRequest<UploadDocumentResponse>
{
    public Stream FileStream { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long FileSize { get; init; }
}
