using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Organization.Contracts;
using Simplia.Organization.UseCases.Abstractions;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;

namespace Simplia.Organization.UseCases.Queries.GetOrganization;

public sealed class GetOrganizationHandler : IRequestHandler<GetOrganizationQuery, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;

    public GetOrganizationHandler(IOrganizationRepository organizations) =>
        _organizations = organizations;

    public async ValueTask<OrganizationResponse> Handle(GetOrganizationQuery request, CancellationToken ct)
    {
        var organization = await _organizations.GetAsync(ct)
            ?? throw new NotFoundException("Organization", OrganizationEntity.ProfileId);

        return OrganizationMapper.ToResponse(organization);
    }
}
