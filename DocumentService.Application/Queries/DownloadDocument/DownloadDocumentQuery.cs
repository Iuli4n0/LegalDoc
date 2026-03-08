using System;
using MediatR;

namespace DocumentService.Application.Queries.DownloadDocument;

public record DownloadDocumentQuery(Guid Id, string UserId) : IRequest<DownloadDocumentResult>;

