using Mediator;
using Simplia.Organization.Contracts;

namespace Simplia.Organization.UseCases.Queries.GetOrganization;

public sealed record GetOrganizationQuery : IRequest<OrganizationResponse>;
