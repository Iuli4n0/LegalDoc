using System;
using MediatR;

namespace DocumentService.Application.Commands.GenerateDocumentClauses;

public record GenerateDocumentClausesCommand(Guid DocumentId)
    : IRequest<global::DocumentService.Application.Commands.GenerateDocumentClauses.GenerateDocumentClausesResponse>;
