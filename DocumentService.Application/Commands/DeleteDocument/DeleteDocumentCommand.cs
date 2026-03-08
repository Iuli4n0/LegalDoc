using System;
using MediatR;

namespace DocumentService.Application.Commands.DeleteDocument;

public record DeleteDocumentCommand(Guid DocumentId, string UserId) : IRequest;

