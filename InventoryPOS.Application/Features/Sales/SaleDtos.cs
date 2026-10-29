using FluentValidation;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Application.Features.Sales;

/// <summary>UnitPrice null uses the variant's list price.</summary>
public record SaleItemSaveDto(int VariantId, decimal Quantity, decimal? UnitPrice, decimal Discount = 0);

/// <summary>Amount is what the customer hands over; cash may exceed the total and produces change.</summary>
public record SalePaymentSaveDto(string Method, decimal Amount, string? Reference);

public record SaleCreateDto(int? CustomerId, decimal Discount, string? Notes, List<SaleItemSaveDto> Items, List<SalePaymentSaveDto> Payments);

public record SaleCancelDto(string Reason);

public record SaleItemDto(int Id, int VariantId, string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal Subtotal);
public record SalePaymentDto(string Method, decimal Amount, string? Reference);

public record SaleDto(
    int Id, string Folio, DateTime CreatedAt, int? CustomerId, string CustomerName, int CashSessionId,
    decimal Subtotal, decimal Discount, decimal Tax, decimal Total, decimal AmountPaid, decimal Change,
    string PaymentMethod, string Status, string? Notes, string? CancelReason, DateTime? CancelledAt, string? UserId, int ItemCount,
    IReadOnlyList<SaleItemDto>? Items, IReadOnlyList<SalePaymentDto>? Payments);

internal static class SaleMappings
{
    public const string WalkInCustomer = "Mostrador";
    public const string MixedPayment = "Mixto";

    public static SaleDto ToDto(this Sale s, bool withDetails)
    {
        var methods = s.Payments.Select(p => p.Method).Distinct().ToList();
        return new SaleDto(
            s.Id, s.Folio, s.CreatedAt, s.CustomerId, s.Customer?.Name ?? WalkInCustomer, s.CashSessionId,
            s.Subtotal, s.Discount, s.Tax, s.Total, s.AmountPaid, s.Change,
            methods.Count == 1 ? methods[0] : methods.Count == 0 ? "-" : MixedPayment,
            s.Status, s.Notes, s.CancelReason, s.CancelledAt, s.UserId, s.Items.Count,
            withDetails ? s.Items.Select(i => new SaleItemDto(i.Id, i.VariantId, i.Description, i.Quantity, i.UnitPrice, i.Discount, i.Subtotal)).ToList() : null,
            withDetails ? s.Payments.Select(p => new SalePaymentDto(p.Method, p.Amount, p.Reference)).ToList() : null);
    }
}

public class SaleCreateValidator : AbstractValidator<SaleCreateDto>
{
    public SaleCreateValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("La venta debe tener al menos un producto.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).When(i => i.UnitPrice.HasValue).WithMessage("El precio no puede ser negativo.");
            item.RuleFor(i => i.Discount).GreaterThanOrEqualTo(0).WithMessage("El descuento no puede ser negativo.");
        });
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("El descuento no puede ser negativo.");
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Registra al menos un pago.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(i => i.Method).Must(m => PaymentMethods.Normalize(m) != null)
                .WithMessage($"Forma de pago inválida. Usa: {string.Join(", ", PaymentMethods.All)}.");
            p.RuleFor(i => i.Amount).GreaterThan(0).WithMessage("El monto del pago debe ser mayor a cero.");
            p.RuleFor(i => i.Reference).MaximumLength(100);
        });
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SaleCancelValidator : AbstractValidator<SaleCancelDto>
{
    public SaleCancelValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indica el motivo de la cancelación.").MaximumLength(300);
    }
}
