using Microsoft.AspNetCore.Identity;

namespace SimpliaDz.Identity.Infrastructure.Data.Entities;

public sealed class AppRole : IdentityRole<Guid>
{
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
