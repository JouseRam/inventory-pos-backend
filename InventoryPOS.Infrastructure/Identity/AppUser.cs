using Microsoft.AspNetCore.Identity;

namespace InventoryPOS.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    /// <summary>Bypasses module permissions and is the only role allowed to manage users.</summary>
    public bool IsSuperAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
