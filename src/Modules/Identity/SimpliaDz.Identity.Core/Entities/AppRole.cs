using Microsoft.AspNetCore.Identity;

namespace SimpliaDz.Identity.Core.Entities;

public sealed class AppRole : IdentityRole<Guid>
{
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
