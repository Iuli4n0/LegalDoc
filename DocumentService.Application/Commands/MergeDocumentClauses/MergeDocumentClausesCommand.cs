using System;
using MediatR;

namespace DocumentService.Application.Commands.MergeDocumentClauses;

public record MergeDocumentClausesCommand(Guid DocumentId, string UserId, Guid FirstClauseId, Guid SecondClauseId) : IRequest<MergeDocumentClausesResponse>;
