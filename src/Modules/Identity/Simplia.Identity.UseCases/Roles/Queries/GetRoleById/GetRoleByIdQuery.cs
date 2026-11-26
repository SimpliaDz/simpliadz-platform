using Mediator;
using Simplia.Identity.Contracts.Roles;

namespace Simplia.Identity.UseCases.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<RoleWithPermissionsResponse>;
