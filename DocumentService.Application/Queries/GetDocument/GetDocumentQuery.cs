using System;
using MediatR;

namespace DocumentService.Application.Queries.GetDocument;

public record GetDocumentQuery(Guid Id) : IRequest<GetDocumentResponse?>;

