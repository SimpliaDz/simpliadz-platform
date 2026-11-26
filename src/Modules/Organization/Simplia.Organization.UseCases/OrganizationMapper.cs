using Simplia.Organization.Contracts;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;

namespace Simplia.Organization.UseCases;

internal static class OrganizationMapper
{
    public static OrganizationResponse ToResponse(OrganizationEntity organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.LegalName,
            organization.Phone,
            organization.Email,
            organization.Address,
            organization.CurrencyCode,
            organization.DefaultLanguage,
            organization.IsActive,
            organization.CreatedAt,
            organization.UpdatedAt,
            organization.TimeZoneId);
}
