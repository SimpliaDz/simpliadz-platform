using Mediator;
using Simplia.Organization.Contracts;
using Simplia.Organization.UseCases.Abstractions;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;

namespace Simplia.Organization.UseCases.Commands.UpdateOrganization;

public sealed class UpdateOrganizationHandler : IRequestHandler<UpdateOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;

    public UpdateOrganizationHandler(IOrganizationRepository organizations) =>
        _organizations = organizations;

    public async ValueTask<OrganizationResponse> Handle(UpdateOrganizationCommand request, CancellationToken ct)
    {
        var organization = await _organizations.GetAsync(ct);

        if (organization is null)
        {
            organization = new OrganizationEntity(
                request.Name,
                request.LegalName,
                request.Phone,
                request.Email,
                request.Address,
                request.CurrencyCode,
                request.DefaultLanguage,
                request.IsActive,
                request.TimeZoneId);

            await _organizations.AddAsync(organization, ct);
        }
        else
        {
            organization.Update(
                request.Name,
                request.LegalName,
                request.Phone,
                request.Email,
                request.Address,
                request.CurrencyCode,
                request.DefaultLanguage,
                request.IsActive,
                request.TimeZoneId);
        }

        await _organizations.SaveChangesAsync(ct);
        return OrganizationMapper.ToResponse(organization);
    }
}
