using Mediator;
using Simplia.Identity.Contracts.Users;

namespace Simplia.Identity.UseCases.Users.Queries.GetUsers;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserResponse>>;
