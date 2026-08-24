using MediatR;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Organization.Contracts;
using SimpliaDz.Organization.UseCases.Abstractions;
using OrganizationEntity = SimpliaDz.Organization.Core.Entities.Organization;

namespace SimpliaDz.Organization.UseCases.Queries.GetOrganization;

public sealed class GetOrganizationHandler : IRequestHandler<GetOrganizationQuery, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;

    public GetOrganizationHandler(IOrganizationRepository organizations) =>
        _organizations = organizations;

    public async Task<OrganizationResponse> Handle(GetOrganizationQuery request, CancellationToken ct)
    {
        var organization = await _organizations.GetAsync(ct)
            ?? throw new NotFoundException("Organization", OrganizationEntity.ProfileId);

        return OrganizationMapper.ToResponse(organization);
    }
}
