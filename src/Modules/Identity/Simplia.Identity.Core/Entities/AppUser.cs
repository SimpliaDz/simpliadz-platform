using Microsoft.AspNetCore.Identity;

namespace Simplia.Identity.Core.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string LanguageCode { get; set; } = "en";
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
