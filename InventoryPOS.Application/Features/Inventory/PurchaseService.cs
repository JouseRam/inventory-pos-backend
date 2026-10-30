using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Inventory;

public class PurchaseService(IUnitOfWork uow, StockService stock, IFolioGenerator folios, ICurrentUser currentUser, IDateTimeProvider clock, PosGuard guard)
{
    public async Task<PagedResult<PurchaseDto>> ListAsync(DateTime? from, DateTime? to, int? supplierId, int page, int pageSize,
        CancellationToken ct = default)
    {
        DateTime? fromUtc = from.HasValue ? clock.ToUtc(from.Value.Date) : null;
        DateTime? toUtc = to.HasValue ? clock.ToUtc(to.Value.Date.AddDays(1)) : null;
        var result = await uow.Purchases.GetPagedAsync(fromUtc, toUtc, supplierId, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return result.Map(p => p.ToDto(withItems: false));
    }

    public async Task<PurchaseDto> GetAsync(int id, CancellationToken ct = default) =>
        (await uow.Purchases.GetWithItemsAsync(id, ct) ?? throw NotFoundException.For("Recepción", id)).ToDto(withItems: true);

    /// <summary>Receives goods: adds stock by lot, records the kardex and updates the variant's last cost.</summary>
    public async Task<PurchaseDto> ReceiveAsync(PurchaseCreateDto dto, CancellationToken ct = default)
    {
        guard.EnsureCanOperate();
        if (dto.SupplierId.HasValue && !await uow.Repository<Supplier>().AnyAsync(s => s.Id == dto.SupplierId, ct))
            throw NotFoundException.For("Proveedor", dto.SupplierId.Value);

        var variants = (await uow.Variants.GetByIdsWithProductAsync(dto.Items.Select(i => i.VariantId).Distinct(), ct))
            .ToDictionary(v => v.Id);

        var purchaseId = await uow.ExecuteInTransactionAsync(async () =>
        {
            var purchase = new Purchase
            {
                Folio = await folios.NextAsync(FolioSeries.Purchase, ct),
                SupplierId = dto.SupplierId,
                InvoiceNumber = dto.InvoiceNumber?.Trim(),
                Notes = dto.Notes?.Trim(),
                UserId = currentUser.UserId,
            };

            foreach (var line in dto.Items)
            {
                if (!variants.TryGetValue(line.VariantId, out var variant))
                    throw NotFoundException.For("Variante", line.VariantId);
                if (variant.Product.TracksBatches && string.IsNullOrWhiteSpace(line.LotNumber))
                    throw new DomainException($"'{variant.DisplayName()}' se controla por lote: captura el lote y la caducidad.");

                var batch = await stock.AddAsync(variant, line.Quantity, line.UnitCost, MovementTypes.Purchase, purchase.Folio,
                    line.LotNumber, line.ExpirationDate, dto.InvoiceNumber, ct);
                variant.Cost = Money.Round(line.UnitCost);

                purchase.Items.Add(new PurchaseItem
                {
                    Variant = variant,
                    Batch = batch,
                    Quantity = line.Quantity,
                    UnitCost = Money.Round(line.UnitCost),
                    Subtotal = Money.Round(line.Quantity * line.UnitCost),
                    LotNumber = batch?.LotNumber ?? line.LotNumber?.Trim(),
                    ExpirationDate = line.ExpirationDate?.Date,
                });
            }

            purchase.Total = purchase.Items.Sum(i => i.Subtotal);
            uow.Purchases.Add(purchase);
            await uow.SaveChangesAsync(ct);
            return purchase.Id;
        }, ct);

        return await GetAsync(purchaseId, ct);
    }
}
