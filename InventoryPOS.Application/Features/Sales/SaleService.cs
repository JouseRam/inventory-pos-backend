using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Sales;

public class SaleService(
    IUnitOfWork uow, StockService stock, IFolioGenerator folios, SettingService settings,
    ICurrentUser currentUser, IDateTimeProvider clock)
{
    public async Task<PagedResult<SaleDto>> ListAsync(DateTime? from, DateTime? to, string? paymentMethod, int? customerId,
        int page, int pageSize, CancellationToken ct = default)
    {
        DateTime? fromUtc = from.HasValue ? clock.ToUtc(from.Value.Date) : null;
        DateTime? toUtc = to.HasValue ? clock.ToUtc(to.Value.Date.AddDays(1)) : null;
        var result = await uow.Sales.GetPagedAsync(fromUtc, toUtc, PaymentMethods.Normalize(paymentMethod), customerId,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return result.Map(s => s.ToDto(withDetails: false));
    }

    public async Task<SaleDto> GetAsync(int id, CancellationToken ct = default) =>
        (await uow.Sales.GetWithDetailsAsync(id, ct) ?? throw NotFoundException.For("Venta", id)).ToDto(withDetails: true);

    /// <summary>
    /// Registers a sale in the cashier's open session: reserves the folio (which serializes concurrent sales),
    /// takes stock FEFO, records payments and change, all in one transaction.
    /// </summary>
    public async Task<SaleDto> CreateAsync(SaleCreateDto dto, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var session = await uow.CashSessions.GetOpenByUserAsync(userId, ct)
            ?? throw new DomainException("Abre la caja antes de registrar ventas.");
        if (dto.CustomerId.HasValue && !await uow.Repository<Customer>().AnyAsync(c => c.Id == dto.CustomerId && c.IsActive, ct))
            throw NotFoundException.For("Cliente", dto.CustomerId.Value);
        var taxRate = await settings.GetDecimalAsync(SettingKeys.TaxRate, 16m, ct);

        var saleId = await uow.ExecuteInTransactionAsync(async () =>
        {
            var folio = await folios.NextAsync(FolioSeries.Sale, ct);
            var variants = (await uow.Variants.GetByIdsWithProductAsync(dto.Items.Select(i => i.VariantId).Distinct(), ct))
                .ToDictionary(v => v.Id);

            var sale = new Sale { Folio = folio, CustomerId = dto.CustomerId, CashSessionId = session.Id, Notes = dto.Notes?.Trim(), UserId = userId };
            foreach (var line in dto.Items)
            {
                if (!variants.TryGetValue(line.VariantId, out var variant) || !variant.IsActive || !variant.Product.IsActive)
                    throw new DomainException($"El producto {line.VariantId} no existe o está inactivo.");

                var unitPrice = Money.Round(line.UnitPrice ?? variant.Price);
                var subtotal = Money.Round(line.Quantity * unitPrice - line.Discount);
                if (subtotal < 0) throw new DomainException($"El descuento de '{variant.DisplayName()}' excede su importe.");

                sale.Items.Add(new SaleItem
                {
                    Variant = variant,
                    Description = variant.DisplayName(),
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    UnitCost = variant.Cost,
                    Discount = Money.Round(line.Discount),
                    Subtotal = subtotal,
                });
                await stock.RemoveAsync(variant, line.Quantity, MovementTypes.Sale, folio, null, ct);
            }

            sale.Subtotal = sale.Items.Sum(i => i.Subtotal);
            sale.Discount = Money.Round(dto.Discount);
            if (sale.Discount > sale.Subtotal) throw new DomainException("El descuento general excede el subtotal.");
            sale.Total = sale.Subtotal - sale.Discount;
            sale.Tax = taxRate > 0 ? Money.Round(sale.Total - sale.Total / (1 + taxRate / 100m)) : 0;
            ApplyPayments(sale, dto.Payments);

            uow.Sales.Add(sale);
            await uow.SaveChangesAsync(ct);
            return sale.Id;
        }, ct);

        return await GetAsync(saleId, ct);
    }

    /// <summary>Cancels a sale while its cash session is open, returning the stock to the original lots.</summary>
    public async Task<SaleDto> CancelAsync(int id, SaleCancelDto dto, CancellationToken ct = default)
    {
        var sale = await uow.Sales.GetWithDetailsAsync(id, ct) ?? throw NotFoundException.For("Venta", id);
        if (sale.Status == SaleStatus.Cancelled) throw new DomainException("La venta ya está cancelada.");
        var session = await uow.CashSessions.GetByIdAsync(sale.CashSessionId, ct);
        if (session?.Status != CashSessionStatus.Open)
            throw new DomainException("La caja de esta venta ya se cerró; no se puede cancelar.");

        await uow.ExecuteInTransactionAsync(async () =>
        {
            var variants = (await uow.Variants.GetByIdsWithProductAsync(sale.Items.Select(i => i.VariantId).Distinct(), ct))
                .ToDictionary(v => v.Id);
            await stock.ReverseAsync(sale.Folio, MovementTypes.Sale, MovementTypes.SaleCancel, variants, dto.Reason.Trim(), ct);

            sale.Status = SaleStatus.Cancelled;
            sale.CancelReason = dto.Reason.Trim();
            sale.CancelledAt = DateTime.UtcNow;
            sale.UpdatedAt = DateTime.UtcNow;
            return await uow.SaveChangesAsync(ct);
        }, ct);

        return sale.ToDto(withDetails: true);
    }

    /// <summary>
    /// Card and transfer amounts are applied exactly and cannot exceed the total; cash covers the rest
    /// and any excess becomes change. Stored payment amounts add up to the sale total.
    /// </summary>
    internal static void ApplyPayments(Sale sale, IEnumerable<SalePaymentSaveDto> payments)
    {
        var normalized = payments.Select(p => (Method: PaymentMethods.Normalize(p.Method)!, Amount: Money.Round(p.Amount), p.Reference)).ToList();
        var cash = normalized.Where(p => p.Method == PaymentMethods.Cash).Sum(p => p.Amount);
        var nonCash = normalized.Where(p => p.Method != PaymentMethods.Cash).ToList();
        var nonCashTotal = nonCash.Sum(p => p.Amount);

        if (nonCashTotal > sale.Total)
            throw new DomainException("Los pagos con tarjeta o transferencia no pueden exceder el total.");
        var tendered = cash + nonCashTotal;
        if (tendered < sale.Total)
            throw new DomainException($"Pago insuficiente: faltan ${sale.Total - tendered:N2}.");

        sale.AmountPaid = tendered;
        sale.Change = tendered - sale.Total;
        foreach (var p in nonCash)
            sale.Payments.Add(new SalePayment { Method = p.Method, Amount = p.Amount, Reference = p.Reference?.Trim() });
        var cashApplied = cash - sale.Change;
        if (cashApplied > 0)
            sale.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = cashApplied });
    }
}
