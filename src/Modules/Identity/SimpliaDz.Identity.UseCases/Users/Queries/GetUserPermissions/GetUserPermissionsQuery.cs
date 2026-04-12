using MediatR;
using SimpliaDz.Identity.Contracts.Users;

namespace SimpliaDz.Identity.UseCases.Users.Queries.GetUserPermissions;

public sealed record GetUserPermissionsQuery(Guid UserId) : IRequest<UserPermissionsResponse>;
