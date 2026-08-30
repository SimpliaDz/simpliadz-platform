using Mediator;
using SimpliaDz.Identity.Contracts.Roles;

namespace SimpliaDz.Identity.UseCases.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleResponse>>;
