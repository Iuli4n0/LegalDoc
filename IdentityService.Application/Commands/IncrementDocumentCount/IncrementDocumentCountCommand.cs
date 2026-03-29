using System;
using MediatR;

namespace IdentityService.Application.Commands.IncrementDocumentCount;

public record IncrementDocumentCountCommand(Guid UserId) : IRequest<IncrementDocumentCountResponse>;

public record IncrementDocumentCountResponse(
    int TotalDocumentsUploaded,
    int MaxDocuments,
    bool CanUpload
);
