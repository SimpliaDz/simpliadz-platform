using OrganizationEntity = SimpliaDz.Organization.Core.Entities.Organization;

namespace SimpliaDz.Organization.UseCases.Abstractions;

public interface IOrganizationRepository
{
    Task<OrganizationEntity?> GetAsync(CancellationToken ct);
    Task AddAsync(OrganizationEntity organization, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
