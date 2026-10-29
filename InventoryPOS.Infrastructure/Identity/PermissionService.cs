using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Identity;

/// <summary>
/// Reads the user from the database on every check, so deactivating an account or revoking
/// a module takes effect immediately even while the user's token is still valid.
/// </summary>
public class PermissionService(ApplicationDbContext db) : IPermissionService
{
    public async Task<bool> HasAccessAsync(string userId, string module, bool requireEdit, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.IsSuperAdmin })
            .FirstOrDefaultAsync(ct);
        if (user is not { IsActive: true }) return false;
        if (user.IsSuperAdmin) return true;
        if (module == PosModules.Users) return false;

        var permission = await db.UserModulePermissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Module == module, ct);
        return requireEdit ? permission?.CanEdit == true : permission?.CanView == true;
    }
}
