using Mediator;
using SimpliaDz.Identity.Contracts.Roles;

namespace SimpliaDz.Identity.UseCases.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IRequest<RoleWithPermissionsResponse>;
