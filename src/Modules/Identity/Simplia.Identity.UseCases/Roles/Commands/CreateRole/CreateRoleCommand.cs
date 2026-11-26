using Mediator;

namespace Simplia.Identity.UseCases.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(string Name) : IRequest<Guid>;
