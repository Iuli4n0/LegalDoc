using System;
using MediatR;

namespace DocumentService.Application.Commands.ClassifyDocumentClauses;

public record ClassifyDocumentClausesCommand(Guid DocumentId) : IRequest<ClassifyDocumentClausesResponse>;

