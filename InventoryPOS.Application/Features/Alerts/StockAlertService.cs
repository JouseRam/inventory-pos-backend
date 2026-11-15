using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Alerts;

public record StockAlertDto(int Id, int VariantId, string Description, string? Sku, string? SupplierName, decimal Stock, decimal MinStock,
    bool OutOfStock, DateTime CreatedAt, bool Read);

public record StockAlertsDto(int Unread, IReadOnlyList<StockAlertDto> Alerts);

/// <summary>
/// Notifications for the administrator when a presentation reaches its minimum. Alerts are reconciled with the stock on each read,
/// so they appear whatever moved the stock (a sale, an adjustment, a sync from another register) and close by themselves when
/// stock comes back above the minimum.
/// </summary>
public class StockAlertService(IUnitOfWork uow, ICurrentUser user)
{
    public async Task<StockAlertsDto> GetAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        var low = (await uow.Variants.GetLowStockAsync(null, ct)).ToDictionary(v => v.Id);
        var alerts = uow.Repository<StockAlert>();
        var open = (await alerts.ListAsync(a => a.ResolvedAt == null, ct)).ToList();
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var alert in open.ToList())
        {
            if (low.TryGetValue(alert.VariantId, out var v) && !open.Any(o => o.VariantId == alert.VariantId && o.Id < alert.Id))
            {
                if (alert.Stock != v.Stock || alert.MinStock != v.MinStock)
                {
                    // Running out after the alert was seen deserves a new look.
                    if (v.Stock <= 0 && alert.Stock > 0) alert.ReadAt = null;
                    alert.Stock = v.Stock;
                    alert.MinStock = v.MinStock;
                    alert.UpdatedAt = now;
                    changed = true;
                }
                continue;
            }
            alert.ResolvedAt = now; // back above the minimum (or a duplicate)
            open.Remove(alert);
            changed = true;
        }
        foreach (var v in low.Values.Where(v => open.All(a => a.VariantId != v.Id)))
        {
            var alert = new StockAlert { VariantId = v.Id, Stock = v.Stock, MinStock = v.MinStock, CreatedAt = now };
            alerts.Add(alert);
            open.Add(alert);
            changed = true;
        }
        if (changed) await uow.SaveChangesAsync(ct);

        var list = open.Select(a =>
        {
            var v = low[a.VariantId];
            return new StockAlertDto(a.Id, v.Id, v.DisplayName(), v.SKU, v.Product.Supplier?.Name, v.Stock, v.MinStock, v.Stock <= 0,
                a.CreatedAt, a.ReadAt != null);
        }).OrderBy(a => a.Read).ThenByDescending(a => a.OutOfStock).ThenByDescending(a => a.CreatedAt).ToList();
        return new StockAlertsDto(list.Count(a => !a.Read), list);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        var now = DateTime.UtcNow;
        foreach (var alert in await uow.Repository<StockAlert>().ListAsync(a => a.ResolvedAt == null && a.ReadAt == null, ct))
            alert.ReadAt = now;
        await uow.SaveChangesAsync(ct);
    }

    private void RequireAdmin()
    {
        if (!user.IsSuperAdmin) throw new ForbiddenException("Solo el administrador recibe las alertas de existencias.");
    }
}
