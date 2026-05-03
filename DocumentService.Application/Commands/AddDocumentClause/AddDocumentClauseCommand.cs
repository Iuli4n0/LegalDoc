using System;
using MediatR;

namespace DocumentService.Application.Commands.AddDocumentClause;

public record AddDocumentClauseCommand(Guid DocumentId, string UserId, string Text) : IRequest<AddDocumentClauseResponse>;
