using Mediator;
using SimpliaDz.Identity.Contracts.Users;

namespace SimpliaDz.Identity.UseCases.Users.Queries.GetUsers;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<UserResponse>>;
