using Mediator;
using SimpliaDz.Identity.Contracts.Users;

namespace SimpliaDz.Identity.UseCases.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserResponse>;
