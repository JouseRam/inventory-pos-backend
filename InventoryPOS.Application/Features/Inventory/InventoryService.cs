using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Inventory;

public class InventoryService(IUnitOfWork uow, StockService stock, IDateTimeProvider clock, PosGuard guard)
{
    public const string AdjustmentReference = "AJUSTE";

    public async Task<PagedResult<MovementDto>> GetMovementsAsync(int? variantId, DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var result = await uow.Movements.GetPagedAsync(variantId, fromUtc, toUtc, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return result.Map(m => m.ToDto());
    }

    public async Task<IReadOnlyList<BatchDto>> GetBatchesAsync(int variantId, CancellationToken ct = default)
    {
        var batches = await uow.Batches.GetAvailableFefoAsync(variantId, ct);
        return batches.Select(b => b.ToDto(clock.LocalToday)).ToList();
    }

    /// <summary>Batches with stock that expire within the given days (already expired included).</summary>
    public async Task<IReadOnlyList<BatchDto>> GetExpiringAsync(int days, CancellationToken ct = default)
    {
        var until = clock.LocalToday.AddDays(Math.Clamp(days, 0, 3650));
        var batches = await uow.Batches.GetExpiringAsync(until, ct);
        return batches.Select(b => b.ToDto(clock.LocalToday)).ToList();
    }

    public async Task<MovementDto> AdjustAsync(AdjustmentDto dto, CancellationToken ct = default)
    {
        guard.EnsureCanOperate();
        var variant = await uow.Variants.GetWithProductAsync(dto.VariantId, ct) ?? throw NotFoundException.For("Variante", dto.VariantId);
        await uow.ExecuteInTransactionAsync(async () =>
        {
            if (dto.Quantity > 0)
                await stock.AddAsync(variant, dto.Quantity, variant.Cost, MovementTypes.AdjustmentIn, AdjustmentReference,
                    dto.LotNumber, dto.ExpirationDate, dto.Reason.Trim(), ct);
            else if (!string.IsNullOrWhiteSpace(dto.LotNumber) && variant.Product.TracksBatches)
                await stock.RemoveFromLotAsync(variant, -dto.Quantity, dto.LotNumber, MovementTypes.AdjustmentOut, AdjustmentReference, dto.Reason.Trim(), ct);
            else
                await stock.RemoveAsync(variant, -dto.Quantity, MovementTypes.AdjustmentOut, AdjustmentReference, dto.Reason.Trim(), ct);
            return await uow.SaveChangesAsync(ct);
        }, ct);

        var latest = await uow.Movements.GetPagedAsync(variant.Id, null, null, 1, 1, ct);
        return latest.Data[0].ToDto();
    }

    private (DateTime? FromUtc, DateTime? ToUtc) ToUtcRange(DateTime? from, DateTime? to) =>
        (from.HasValue ? clock.ToUtc(from.Value.Date) : null, to.HasValue ? clock.ToUtc(to.Value.Date.AddDays(1)) : null);
}
