using System;
using MediatR;

namespace DocumentService.Application.Commands.IndexDocument;

public record IndexDocumentCommand(Guid DocumentId) : IRequest<IndexDocumentResponse>;
