using System;
using MediatR;

namespace IdentityService.Application.Queries.CheckUserLimits;

public record CheckUserLimitsQuery(Guid UserId) : IRequest<CheckUserLimitsResponse>;

public record CheckUserLimitsResponse(
    int TotalDocumentsUploaded,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    bool CanUpload
);
