using Mediator;
using SimpliaDz.Organization.Contracts;

namespace SimpliaDz.Organization.UseCases.Queries.GetOrganization;

public sealed record GetOrganizationQuery : IRequest<OrganizationResponse>;
