using FluentValidation;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Application.Features.Inventory;

public record MovementDto(
    int Id, DateTime CreatedAt, int VariantId, string Description, string SKU, string Type,
    decimal Quantity, decimal StockAfter, decimal UnitCost, string? LotNumber, string? Reference, string? Notes, string? UserId);

public record BatchDto(
    int Id, int VariantId, string Description, string SKU, string LotNumber, DateTime? ExpirationDate,
    decimal Quantity, int? DaysToExpire);

/// <summary>Signed quantity: positive adds stock, negative removes it.</summary>
public record AdjustmentDto(int VariantId, decimal Quantity, string Reason, string? LotNumber, DateTime? ExpirationDate);

public record PurchaseItemSaveDto(int VariantId, decimal Quantity, decimal UnitCost, string? LotNumber, DateTime? ExpirationDate);
public record PurchaseCreateDto(int? SupplierId, string? InvoiceNumber, string? Notes, List<PurchaseItemSaveDto> Items);

public record PurchaseItemDto(int Id, int VariantId, string Description, string SKU, decimal Quantity, decimal UnitCost, decimal Subtotal,
    string? LotNumber, DateTime? ExpirationDate);
public record PurchaseDto(int Id, string Folio, DateTime CreatedAt, int? SupplierId, string? SupplierName, string? InvoiceNumber,
    decimal Total, string? Notes, int ItemCount, IReadOnlyList<PurchaseItemDto>? Items);

internal static class InventoryMappings
{
    public static MovementDto ToDto(this InventoryMovement m) => new(
        m.Id, m.CreatedAt, m.VariantId, m.Variant.DisplayName(), m.Variant.SKU, m.Type,
        m.Quantity, m.StockAfter, m.UnitCost, m.Batch?.LotNumber, m.Reference, m.Notes, m.UserId);

    public static BatchDto ToDto(this ProductBatch b, DateTime localToday) => new(
        b.Id, b.VariantId, b.Variant.DisplayName(), b.Variant.SKU, b.LotNumber, b.ExpirationDate, b.Quantity,
        b.ExpirationDate.HasValue ? (int)(b.ExpirationDate.Value.Date - localToday).TotalDays : null);

    public static PurchaseDto ToDto(this Purchase p, bool withItems) => new(
        p.Id, p.Folio, p.CreatedAt, p.SupplierId, p.Supplier?.Name, p.InvoiceNumber, p.Total, p.Notes, p.Items.Count,
        withItems
            ? p.Items.Select(i => new PurchaseItemDto(i.Id, i.VariantId, i.Variant.DisplayName(), i.Variant.SKU,
                i.Quantity, i.UnitCost, i.Subtotal, i.LotNumber, i.ExpirationDate)).ToList()
            : null);
}

public class AdjustmentValidator : AbstractValidator<AdjustmentDto>
{
    public AdjustmentValidator()
    {
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("La cantidad del ajuste no puede ser cero.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indica el motivo del ajuste.").MaximumLength(300);
        RuleFor(x => x.LotNumber).MaximumLength(50);
    }
}

public class PurchaseCreateValidator : AbstractValidator<PurchaseCreateDto>
{
    public PurchaseCreateValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("La recepción debe tener al menos un producto.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
            item.RuleFor(i => i.LotNumber).MaximumLength(50);
        });
        RuleFor(x => x.InvoiceNumber).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
