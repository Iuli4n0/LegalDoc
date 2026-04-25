using System;
using MediatR;

namespace IdentityService.Application.Commands.CreatePortalSession;

public record CreatePortalSessionCommand(Guid UserId) : IRequest<CreatePortalSessionResponse>;

public record CreatePortalSessionResponse(string PortalUrl);
