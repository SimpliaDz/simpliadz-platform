using Mediator;

namespace Simplia.Identity.UseCases.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    IList<string>? Roles
    ) : IRequest<Guid>;
