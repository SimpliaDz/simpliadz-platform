using MediatR;
using SimpliaDz.Identity.Contracts.Permissions;

namespace SimpliaDz.Identity.UseCases.Permissions.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionResponse>>;
