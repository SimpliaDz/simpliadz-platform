using Microsoft.EntityFrameworkCore;
using Simplia.Organization.UseCases.Abstractions;
using OrganizationEntity = Simplia.Organization.Core.Entities.Organization;

namespace Simplia.Organization.Infrastructure.Data.Repositories;

internal sealed class EfOrganizationRepository : IOrganizationRepository
{
    private readonly OrganizationDbContext _db;

    public EfOrganizationRepository(OrganizationDbContext db) => _db = db;

    public Task<OrganizationEntity?> GetAsync(CancellationToken ct) =>
        _db.Organizations.SingleOrDefaultAsync(x => x.Id == OrganizationEntity.ProfileId, ct);

    public async Task AddAsync(OrganizationEntity organization, CancellationToken ct) =>
        await _db.Organizations.AddAsync(organization, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
