using System;

namespace DocumentService.Application.Queries.GetDocumentClauses;

public record GetDocumentClausesQuery(Guid DocumentId) : MediatR.IRequest<GetDocumentClausesResponse>;
