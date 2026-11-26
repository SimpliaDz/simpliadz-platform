using Microsoft.EntityFrameworkCore;

using Simplia.Identity.Contracts.Permissions;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Identity.UseCases.Permissions.Abstractions;

namespace Simplia.Identity.Infrastructure.Permissions;

internal sealed class PermissionService : IPermissionService
{
    private readonly IdentityDbContext _db;

    public PermissionService(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionResponse(p.Name, p.DisplayName, p.Category))
            .ToListAsync(ct);
    }
}
