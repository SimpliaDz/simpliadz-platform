using Mediator;
using Simplia.Identity.Contracts.Permissions;

namespace Simplia.Identity.UseCases.Permissions.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionResponse>>;
