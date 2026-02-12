using Microsoft.AspNetCore.Identity;

namespace SimpliaDz.Identity.Infrastructure.Data.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    // IMPORTANT: DB-per-tenant already isolates users physically.
    // Keep user clean for now; add profile fields later (FirstName, etc.)
}
