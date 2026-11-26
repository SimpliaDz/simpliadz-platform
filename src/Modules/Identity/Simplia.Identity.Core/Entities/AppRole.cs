using Microsoft.AspNetCore.Identity;

namespace Simplia.Identity.Core.Entities;

public sealed class AppRole : IdentityRole<Guid>
{
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
