using System;
using MediatR;

namespace IdentityService.Application.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<GetUserByIdResponse?>;

