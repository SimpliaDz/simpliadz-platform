using Mediator;
using Simplia.Identity.Contracts.Users;

namespace Simplia.Identity.UseCases.Users.Queries.GetUserPermissions;

public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<UserPermissionsResponse>;
