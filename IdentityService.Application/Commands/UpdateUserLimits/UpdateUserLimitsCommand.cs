using System;
using MediatR;

namespace IdentityService.Application.Commands.UpdateUserLimits;

public record UpdateUserLimitsCommand(Guid UserId, int MaxDocuments, int MaxDocumentSizeMb) : IRequest;
