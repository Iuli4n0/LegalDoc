using System;
using MediatR;

namespace DocumentService.Application.Commands.DeleteDocumentClause;

public record DeleteDocumentClauseCommand(Guid DocumentId, string UserId, Guid ClauseId) : IRequest;
