using FluentValidation;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Application.Features.Products;

public record VariantDto(
    int Id, int ProductId, string ProductName, string? BrandName, string DisplayName,
    string SKU, string? Barcode, string? ShadeCode, string? ShadeName, decimal? Size, string Unit,
    decimal Price, decimal Cost, decimal Stock, decimal MinStock, bool LowStock, bool TracksBatches, bool IsActive);

public record ProductDto(
    int Id, string Name, string? Description,
    int? BrandId, string? BrandName, int? CategoryId, string? CategoryName, int? SupplierId, string? SupplierName,
    bool TracksBatches, bool IsActive, decimal TotalStock, bool HasLowStock, DateTime CreatedAt, DateTime? UpdatedAt,
    IReadOnlyList<VariantDto> Variants);

public record VariantSaveDto(
    string SKU, string? Barcode, string? ShadeCode, string? ShadeName, decimal? Size, string? Unit,
    decimal Price, decimal Cost, decimal MinStock, bool IsActive = true,
    decimal InitialStock = 0, string? LotNumber = null, DateTime? ExpirationDate = null);

public record ProductCreateDto(
    string Name, string? Description, int? BrandId, int? CategoryId, int? SupplierId, bool TracksBatches,
    List<VariantSaveDto> Variants);

public record ProductUpdateDto(
    string Name, string? Description, int? BrandId, int? CategoryId, int? SupplierId, bool TracksBatches, bool IsActive = true);

internal static class ProductMappings
{
    public static VariantDto ToDto(this ProductVariant v) => new(
        v.Id, v.ProductId, v.Product.Name, v.Product.Brand?.Name, v.DisplayName(),
        v.SKU, v.Barcode, v.ShadeCode, v.ShadeName, v.Size, v.Unit,
        v.Price, v.Cost, v.Stock, v.MinStock, v.Stock <= v.MinStock, v.Product.TracksBatches, v.IsActive);

    public static ProductDto ToDto(this Product p, bool includeInactiveVariants = false)
    {
        var variants = p.Variants.Where(v => includeInactiveVariants || v.IsActive)
            .OrderBy(v => v.ShadeCode).ThenBy(v => v.Size).ThenBy(v => v.SKU)
            .Select(v => v.ToDto()).ToList();
        return new ProductDto(
            p.Id, p.Name, p.Description,
            p.BrandId, p.Brand?.Name, p.CategoryId, p.Category?.Name, p.SupplierId, p.Supplier?.Name,
            p.TracksBatches, p.IsActive, variants.Where(v => v.IsActive).Sum(v => v.Stock),
            variants.Any(v => v.IsActive && v.LowStock), p.CreatedAt, p.UpdatedAt, variants);
    }
}

public class VariantSaveValidator : AbstractValidator<VariantSaveDto>
{
    public VariantSaveValidator()
    {
        RuleFor(x => x.SKU).NotEmpty().WithMessage("El SKU es requerido.").MaximumLength(50);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.ShadeCode).MaximumLength(20);
        RuleFor(x => x.ShadeName).MaximumLength(100);
        RuleFor(x => x.Unit).Must(u => u == null || UnitsOfMeasure.All.Contains(u))
            .WithMessage($"Unidad inválida. Usa: {string.Join(", ", UnitsOfMeasure.All)}.");
        RuleFor(x => x.Size).GreaterThan(0).When(x => x.Size.HasValue).WithMessage("El contenido debe ser mayor a cero.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0).WithMessage("El stock inicial no puede ser negativo.");
    }
}

public class ProductCreateValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Variants).NotEmpty().WithMessage("Agrega al menos una presentación (variante).");
        RuleForEach(x => x.Variants).SetValidator(new VariantSaveValidator());
        RuleFor(x => x.Variants)
            .Must(v => v.Select(x => x.SKU.Trim().ToUpperInvariant()).Distinct().Count() == v.Count)
            .When(x => x.Variants != null && x.Variants.All(v => !string.IsNullOrWhiteSpace(v.SKU)))
            .WithMessage("Hay SKUs repetidos entre las variantes.");
    }
}

public class ProductUpdateValidator : AbstractValidator<ProductUpdateDto>
{
    public ProductUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es requerido.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
