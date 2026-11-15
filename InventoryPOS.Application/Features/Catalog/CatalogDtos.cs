using FluentValidation;
using InventoryPOS.Application.Features.Invoicing;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Application.Features.Catalog;

public record BrandDto(int Id, string Name, bool IsActive);
public record BrandSaveDto(string Name, bool IsActive = true);

public record CategoryDto(int Id, string Name, string? Description, int? ParentId, string? ParentName, bool IsActive);
public record CategorySaveDto(string Name, string? Description, int? ParentId, bool IsActive = true);

public record SupplierDto(int Id, string Name, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive);
public record SupplierSaveDto(string Name, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive = true);

public record CustomerDto(int Id, string Name, string? Phone, string? Email, string? Address, string? Notes, bool IsActive, DateTime CreatedAt,
    string? TaxId, string? LegalName, string? TaxRegime, string? TaxZipCode, string? CfdiUse,
    string Type = CustomerTypes.Retail, decimal DiscountPercent = 0, bool AcceptsPromotions = false);
/// <summary>
/// Fiscal fields are all empty (the customer does not invoice) or all filled. The phone is stored as 10 digits.
/// DiscountPercent applies only to a Mayorista.
/// </summary>
public record CustomerSaveDto(string Name, string? Phone, string? Email, string? Address, string? Notes, bool IsActive = true,
    string? TaxId = null, string? LegalName = null, string? TaxRegime = null, string? TaxZipCode = null, string? CfdiUse = null,
    string? Type = null, decimal DiscountPercent = 0, bool AcceptsPromotions = false);

internal static class CatalogMappings
{
    public static BrandDto ToDto(this Brand b) => new(b.Id, b.Name, b.IsActive);

    public static CategoryDto ToDto(this Category c, string? parentName) =>
        new(c.Id, c.Name, c.Description, c.ParentId, parentName, c.IsActive);

    public static SupplierDto ToDto(this Supplier s) =>
        new(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive);

    public static CustomerDto ToDto(this Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Email, c.Address, c.Notes, c.IsActive, c.CreatedAt,
            c.TaxId, c.LegalName, c.TaxRegime, c.TaxZipCode, c.CfdiUse, c.Type, c.DiscountPercent, c.AcceptsPromotions);
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
        RuleFor(x => x.Phone).Must(p => PhoneNumber.Normalize(p) != null).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("El teléfono debe tener 10 dígitos.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Type).Must(t => CustomerTypes.All.Contains(t)).When(x => x.Type != null)
            .WithMessage("El tipo de cliente es Minorista o Mayorista.");
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).WithMessage("El descuento va de 0 a 100 %.");
        RuleFor(x => x.DiscountPercent).GreaterThan(0).When(x => x.Type == CustomerTypes.Wholesale)
            .WithMessage("Indica el porcentaje de descuento del mayorista.");
        RuleFor(x => x.Phone).NotEmpty().When(x => x.AcceptsPromotions)
            .WithMessage("Para enviarle promociones captura su teléfono.");

        When(x => HasAnyFiscalData(x), () =>
        {
            RuleFor(x => FiscalRules.Normalize(x.TaxId)).Must(FiscalRules.IsValidRfc).OverridePropertyName(nameof(CustomerSaveDto.TaxId))
                .WithMessage("RFC inválido: 12 caracteres para persona moral o 13 para persona física (ej. XAXX010101000).");
            RuleFor(x => x.LegalName).NotEmpty().WithMessage("La razón social es requerida para facturar.").MaximumLength(300);
            RuleFor(x => x.TaxRegime).Must((x, code) => FiscalRules.IsValidTaxRegime(code, FiscalRules.Normalize(x.TaxId)))
                .WithMessage("Elige un régimen fiscal que corresponda al tipo de RFC (persona física o moral).");
            RuleFor(x => x.TaxZipCode).Must(z => FiscalRules.IsValidZip(z?.Trim()))
                .WithMessage("El código postal fiscal debe tener 5 dígitos.");
            RuleFor(x => FiscalRules.Normalize(x.CfdiUse)).Must((x, code) => FiscalRules.IsValidCfdiUse(code, FiscalRules.Normalize(x.TaxId)))
                .OverridePropertyName(nameof(CustomerSaveDto.CfdiUse))
                .WithMessage("Elige un uso de CFDI que corresponda al tipo de RFC (persona física o moral).");
        });
    }

    private static bool HasAnyFiscalData(CustomerSaveDto x) =>
        new[] { x.TaxId, x.LegalName, x.TaxRegime, x.TaxZipCode, x.CfdiUse }.Any(v => !string.IsNullOrWhiteSpace(v));
}
