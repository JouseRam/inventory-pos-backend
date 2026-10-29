using FluentValidation;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Application.Features.Catalog;

public record BrandDto(int Id, string Name, bool IsActive);
public record BrandSaveDto(string Name, bool IsActive = true);

public record CategoryDto(int Id, string Name, string? Description, int? ParentId, string? ParentName, bool IsActive);
public record CategorySaveDto(string Name, string? Description, int? ParentId, bool IsActive = true);

public record SupplierDto(int Id, string Name, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive);
public record SupplierSaveDto(string Name, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive = true);

public record CustomerDto(int Id, string Name, string? Phone, string? Email, string? Address, string? Notes, bool IsActive, DateTime CreatedAt);
public record CustomerSaveDto(string Name, string? Phone, string? Email, string? Address, string? Notes, bool IsActive = true);

internal static class CatalogMappings
{
    public static BrandDto ToDto(this Brand b) => new(b.Id, b.Name, b.IsActive);

    public static CategoryDto ToDto(this Category c, string? parentName) =>
        new(c.Id, c.Name, c.Description, c.ParentId, parentName, c.IsActive);

    public static SupplierDto ToDto(this Supplier s) =>
        new(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive);

    public static CustomerDto ToDto(this Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Email, c.Address, c.Notes, c.IsActive, c.CreatedAt);
}

public class BrandSaveValidator : AbstractValidator<BrandSaveDto>
{
    public BrandSaveValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(100);
    }
}

public class CategorySaveValidator : AbstractValidator<CategorySaveDto>
{
    public CategorySaveValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class SupplierSaveValidator : AbstractValidator<SupplierSaveDto>
{
    public SupplierSaveValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Correo inválido.").When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public class CustomerSaveValidator : AbstractValidator<CustomerSaveDto>
{
    public CustomerSaveValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Correo inválido.").When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
