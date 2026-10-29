using FluentValidation;
using InventoryPOS.Domain.Constants;

namespace InventoryPOS.Application.Features.Users;

public record LoginDto(string UserName, string Password);
public record ModulePermissionDto(string Module, bool CanView, bool CanEdit);
public record ProfileDto(string Id, string UserName, string FullName, string? Email, bool IsSuperAdmin, IReadOnlyList<ModulePermissionDto> Permissions);
public record AuthResponseDto(string Token, DateTime ExpiresAt, ProfileDto User);

public record UserDto(string Id, string UserName, string FullName, string? Email, bool IsActive, bool IsSuperAdmin,
    IReadOnlyList<ModulePermissionDto> Permissions);
public record UserCreateDto(string UserName, string FullName, string? Email, string Password, bool IsSuperAdmin,
    List<ModulePermissionDto>? Permissions);
public record UserUpdateDto(string FullName, string? Email, bool IsActive, bool IsSuperAdmin, string? NewPassword);

/// <summary>User accounts and login; implemented over ASP.NET Core Identity in Infrastructure.</summary>
public interface IIdentityService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<ProfileDto> GetProfileAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(UserCreateDto dto, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(string id, UserUpdateDto dto, CancellationToken ct = default);
    Task<UserDto> SetPermissionsAsync(string id, IReadOnlyList<ModulePermissionDto> permissions, CancellationToken ct = default);
}

/// <summary>Module access check: super admins pass, inactive users never do, everyone else per UserModulePermissions.</summary>
public interface IPermissionService
{
    Task<bool> HasAccessAsync(string userId, string module, bool requireEdit, CancellationToken ct = default);
}

public class LoginValidator : AbstractValidator<LoginDto>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Captura el usuario.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Captura la contraseña.");
    }
}

public class ModulePermissionValidator : AbstractValidator<ModulePermissionDto>
{
    public ModulePermissionValidator()
    {
        RuleFor(x => x.Module).Must(PosModules.IsValid).WithMessage(x => $"Módulo desconocido: {x.Module}.");
    }
}

public class UserCreateValidator : AbstractValidator<UserCreateDto>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("El usuario es requerido.").MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("El usuario solo admite letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Correo inválido.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
        RuleForEach(x => x.Permissions).SetValidator(new ModulePermissionValidator());
    }
}

public class UserUpdateValidator : AbstractValidator<UserUpdateDto>
{
    public UserUpdateValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Correo inválido.");
        RuleFor(x => x.NewPassword).MinimumLength(8).When(x => !string.IsNullOrEmpty(x.NewPassword))
            .WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}
