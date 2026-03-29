using MediatR;

namespace IdentityService.Application.Queries.GetAllUsers;

public record GetAllUsersQuery() : IRequest<GetAllUsersResponse>;
