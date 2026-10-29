using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryPOS.Infrastructure.Identity;

public class IdentityService(UserManager<AppUser> users, ApplicationDbContext db, IOptions<JwtOptions> jwtOptions) : IIdentityService
{
    private const string InvalidCredentials = "Usuario o contraseña incorrectos.";

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await users.FindByNameAsync(dto.UserName.Trim());
        if (user == null || !user.IsActive) throw new DomainException(InvalidCredentials);
        if (await users.IsLockedOutAsync(user))
            throw new DomainException("La cuenta está bloqueada temporalmente por intentos fallidos; intenta más tarde.");
        if (!await users.CheckPasswordAsync(user, dto.Password))
        {
            await users.AccessFailedAsync(user);
            throw new DomainException(InvalidCredentials);
        }
        await users.ResetAccessFailedCountAsync(user);

        var (token, expiresAt) = CreateToken(user);
        return new AuthResponseDto(token, expiresAt, await BuildProfileAsync(user, ct));
    }

    public async Task<ProfileDto> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || !user.IsActive) throw new ForbiddenException("La cuenta no está activa.");
        return await BuildProfileAsync(user, ct);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var all = await users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var permissions = (await db.UserModulePermissions.AsNoTracking().ToListAsync(ct)).ToLookup(p => p.UserId);
        return all.Select(u => ToDto(u, permissions[u.Id])).ToList();
    }

    public async Task<UserDto> CreateAsync(UserCreateDto dto, CancellationToken ct = default)
    {
        var user = new AppUser
        {
            UserName = dto.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            FullName = dto.FullName.Trim(),
            IsSuperAdmin = dto.IsSuperAdmin,
        };
        EnsureSucceeded(await users.CreateAsync(user, dto.Password));
        if (dto.Permissions is { Count: > 0 })
            return await SetPermissionsAsync(user.Id, dto.Permissions, ct);
        return ToDto(user, []);
    }

    public async Task<UserDto> UpdateAsync(string id, UserUpdateDto dto, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id) ?? throw NotFoundException.For("Usuario", id);
        if (user.IsSuperAdmin && (!dto.IsSuperAdmin || !dto.IsActive) && await IsLastActiveSuperAdminAsync(user.Id, ct))
            throw new DomainException("No puedes desactivar ni quitar privilegios al último super administrador.");

        user.FullName = dto.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
        user.IsActive = dto.IsActive;
        user.IsSuperAdmin = dto.IsSuperAdmin;
        EnsureSucceeded(await users.UpdateAsync(user));

        if (!string.IsNullOrEmpty(dto.NewPassword))
        {
            var resetToken = await users.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await users.ResetPasswordAsync(user, resetToken, dto.NewPassword));
        }
        return ToDto(user, await db.UserModulePermissions.AsNoTracking().Where(p => p.UserId == id).ToListAsync(ct));
    }

    /// <summary>Replaces the user's module permissions; CanEdit implies CanView.</summary>
    public async Task<UserDto> SetPermissionsAsync(string id, IReadOnlyList<ModulePermissionDto> permissions, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id) ?? throw NotFoundException.For("Usuario", id);
        var current = await db.UserModulePermissions.Where(p => p.UserId == id).ToListAsync(ct);
        db.UserModulePermissions.RemoveRange(current);

        foreach (var p in permissions.Where(p => PosModules.IsValid(p.Module) && (p.CanView || p.CanEdit)).DistinctBy(p => p.Module))
            db.UserModulePermissions.Add(new UserModulePermission { UserId = id, Module = p.Module, CanView = true, CanEdit = p.CanEdit });

        await db.SaveChangesAsync(ct);
        return ToDto(user, await db.UserModulePermissions.AsNoTracking().Where(p => p.UserId == id).ToListAsync(ct));
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(string userId, CancellationToken ct) =>
        !await users.Users.AnyAsync(u => u.Id != userId && u.IsSuperAdmin && u.IsActive, ct);

    private async Task<ProfileDto> BuildProfileAsync(AppUser user, CancellationToken ct)
    {
        IReadOnlyList<ModulePermissionDto> permissions = user.IsSuperAdmin
            ? PosModules.All.Select(m => new ModulePermissionDto(m.Key, true, true)).Append(new ModulePermissionDto(PosModules.Users, true, true)).ToList()
            : (await db.UserModulePermissions.AsNoTracking().Where(p => p.UserId == user.Id).ToListAsync(ct))
                .Select(p => new ModulePermissionDto(p.Module, p.CanView, p.CanEdit)).ToList();
        return new ProfileDto(user.Id, user.UserName!, user.FullName, user.Email, user.IsSuperAdmin, permissions);
    }

    private (string Token, DateTime ExpiresAt) CreateToken(AppUser user)
    {
        var options = jwtOptions.Value;
        var expiresAt = DateTime.UtcNow.AddHours(options.ExpirationHours);
        var claims = new List<Claim>
        {
            new(PosClaims.UserId, user.Id),
            new(PosClaims.UserName, user.UserName!),
            new(PosClaims.FullName, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (user.IsSuperAdmin) claims.Add(new Claim(PosClaims.SuperAdmin, "true"));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, DateTime.UtcNow, expiresAt, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static UserDto ToDto(AppUser u, IEnumerable<UserModulePermission> permissions) =>
        new(u.Id, u.UserName!, u.FullName, u.Email, u.IsActive, u.IsSuperAdmin,
            permissions.Select(p => new ModulePermissionDto(p.Module, p.CanView, p.CanEdit)).OrderBy(p => p.Module).ToList());

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            throw new ConflictException("El usuario o correo ya está registrado.");
        throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
