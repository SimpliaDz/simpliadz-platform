using Mediator;
using Simplia.Identity.Contracts.Users;

namespace Simplia.Identity.UseCases.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserResponse>;
