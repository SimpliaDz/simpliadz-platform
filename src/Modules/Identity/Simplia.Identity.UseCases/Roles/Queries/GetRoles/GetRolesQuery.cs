using Mediator;
using Simplia.Identity.Contracts.Roles;

namespace Simplia.Identity.UseCases.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleResponse>>;
