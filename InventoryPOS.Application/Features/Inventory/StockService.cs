using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Inventory;

/// <summary>
/// Single entry point for stock changes: updates the variant, its batches and writes the kardex.
/// Does not save; callers persist through the unit of work. Variants must be loaded with their Product.
/// </summary>
public class StockService(IUnitOfWork uow, ICurrentUser currentUser)
{
    /// <summary>Lot used for batch-tracked stock that arrives without a lot number.</summary>
    public const string NoLot = "SIN-LOTE";

    public async Task<ProductBatch?> AddAsync(ProductVariant variant, decimal quantity, decimal unitCost, string type,
        string? reference, string? lotNumber = null, DateTime? expirationDate = null, string? notes = null, CancellationToken ct = default)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a ingresar debe ser mayor a cero.");

        ProductBatch? batch = null;
        if (variant.Product.TracksBatches)
        {
            var lot = string.IsNullOrWhiteSpace(lotNumber) ? NoLot : lotNumber.Trim().ToUpperInvariant();
            batch = await uow.Batches.FindByLotAsync(variant.Id, lot, ct);
            if (batch == null)
            {
                batch = new ProductBatch { Variant = variant, LotNumber = lot, ExpirationDate = expirationDate?.Date };
                uow.Batches.Add(batch);
            }
            else if (expirationDate.HasValue && batch.ExpirationDate == null)
            {
                batch.ExpirationDate = expirationDate.Value.Date;
            }
            batch.Quantity += quantity;
            batch.UpdatedAt = DateTime.UtcNow;
        }

        variant.Stock += quantity;
        variant.UpdatedAt = DateTime.UtcNow;
        Record(variant, batch, type, quantity, unitCost, reference, notes);
        return batch;
    }

    /// <summary>Removes stock, consuming batches first-expired-first-out when the product tracks lots.</summary>
    public async Task RemoveAsync(ProductVariant variant, decimal quantity, string type, string? reference,
        string? notes = null, CancellationToken ct = default)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a retirar debe ser mayor a cero.");
        if (variant.Stock < quantity)
            throw new DomainException($"Stock insuficiente para '{variant.DisplayName()}'. Disponible: {variant.Stock:0.###} {variant.Unit}.");

        if (variant.Product.TracksBatches)
        {
            var remaining = quantity;
            foreach (var batch in await uow.Batches.GetAvailableFefoAsync(variant.Id, ct))
            {
                if (remaining <= 0) break;
                var take = Math.Min(batch.Quantity, remaining);
                batch.Quantity -= take;
                batch.UpdatedAt = DateTime.UtcNow;
                remaining -= take;
                variant.Stock -= take;
                Record(variant, batch, type, -take, variant.Cost, reference, notes);
            }
            if (remaining > 0)
                throw new DomainException($"Los lotes de '{variant.DisplayName()}' no cubren la cantidad solicitada; revisa el inventario por lote.");
        }
        else
        {
            variant.Stock -= quantity;
            Record(variant, null, type, -quantity, variant.Cost, reference, notes);
        }
        variant.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Takes stock out of one specific lot (expired or damaged goods).</summary>
    public async Task RemoveFromLotAsync(ProductVariant variant, decimal quantity, string lotNumber, string type, string? reference,
        string? notes = null, CancellationToken ct = default)
    {
        if (quantity <= 0) throw new DomainException("La cantidad a retirar debe ser mayor a cero.");
        var batch = await uow.Batches.FindByLotAsync(variant.Id, lotNumber.Trim().ToUpperInvariant(), ct)
            ?? throw new NotFoundException($"El lote '{lotNumber}' no existe para '{variant.DisplayName()}'.");
        if (batch.Quantity < quantity)
            throw new DomainException($"El lote '{batch.LotNumber}' solo tiene {batch.Quantity:0.###} {variant.Unit}.");

        batch.Quantity -= quantity;
        batch.UpdatedAt = DateTime.UtcNow;
        variant.Stock -= quantity;
        variant.UpdatedAt = DateTime.UtcNow;
        Record(variant, batch, type, -quantity, variant.Cost, reference, notes);
    }

    /// <summary>Reverses every movement recorded under a reference, returning stock to the same batches.</summary>
    public async Task ReverseAsync(string reference, string originalType, string reverseType, IReadOnlyDictionary<int, ProductVariant> variants,
        string? notes = null, CancellationToken ct = default)
    {
        var movements = await uow.Movements.GetByReferenceAsync(reference, ct);
        foreach (var m in movements.Where(m => m.Type == originalType))
        {
            if (!variants.TryGetValue(m.VariantId, out var variant)) continue;
            var quantity = -m.Quantity;
            if (m.Batch != null)
            {
                m.Batch.Quantity += quantity;
                m.Batch.UpdatedAt = DateTime.UtcNow;
            }
            variant.Stock += quantity;
            variant.UpdatedAt = DateTime.UtcNow;
            Record(variant, m.Batch, reverseType, quantity, m.UnitCost, reference, notes);
        }
    }

    private void Record(ProductVariant variant, ProductBatch? batch, string type, decimal quantity, decimal unitCost, string? reference, string? notes)
    {
        uow.Movements.Add(new InventoryMovement
        {
            Variant = variant,
            Batch = batch,
            Type = type,
            Quantity = quantity,
            StockAfter = variant.Stock,
            UnitCost = unitCost,
            Reference = reference,
            Notes = notes,
            UserId = currentUser.UserId,
        });
    }
}
