using InventoryPOS.Application.Features.Reports;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Services;

/// <summary>
/// Report aggregations. Filtering runs in the database; sums over money run in memory on narrow rows
/// because SQLite (the desktop store) cannot aggregate or sort decimal columns. A single store's
/// yearly volume fits comfortably in memory.
/// </summary>
public class ReportQueries(ApplicationDbContext db) : IReportQueries
{
    private record ItemRow(int VariantId, string Description, decimal Quantity, decimal Subtotal, decimal UnitCost, int? CategoryId, int? BrandId);

    private Task<List<ItemRow>> CompletedItemsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        db.SaleItems.AsNoTracking()
            .Where(i => i.Sale.CreatedAt >= fromUtc && i.Sale.CreatedAt < toUtc && i.Sale.Status != SaleStatus.Cancelled)
            .Select(i => new ItemRow(i.VariantId, i.Description, i.Quantity, i.Subtotal, i.UnitCost, i.Variant.Product.CategoryId, i.Variant.Product.BrandId))
            .ToListAsync(ct);

    public async Task<SalesTotals> GetSalesTotalsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc && s.Status != SaleStatus.Cancelled)
            .Select(s => new { s.Total, s.Discount, s.Tax })
            .ToListAsync(ct);
        var items = await CompletedItemsAsync(fromUtc, toUtc, ct);
        return new SalesTotals(sales.Count, sales.Sum(s => s.Total), sales.Sum(s => s.Discount), sales.Sum(s => s.Tax),
            Math.Round(items.Sum(i => i.UnitCost * i.Quantity), 2));
    }

    public async Task<IReadOnlyList<(DateTime CreatedAtUtc, decimal Total)>> GetSaleTotalsByTimeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await db.Sales.AsNoTracking()
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc && s.Status != SaleStatus.Cancelled)
            .Select(s => new { s.CreatedAt, s.Total })
            .ToListAsync(ct);
        return rows.Select(r => (r.CreatedAt, r.Total)).ToList();
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default) =>
        (await CompletedItemsAsync(fromUtc, toUtc, ct))
            .GroupBy(i => new { i.VariantId, i.Description })
            .Select(g => new TopProductDto(g.Key.VariantId, g.Key.Description, g.Sum(i => i.Quantity), g.Sum(i => i.Subtotal)))
            .OrderByDescending(x => x.TotalRevenue)
            .Take(limit)
            .ToList();

    /// <summary>Totals per top-level category (subcategories roll up into their parent).</summary>
    public async Task<IReadOnlyList<GroupTotalDto>> GetByCategoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var items = await CompletedItemsAsync(fromUtc, toUtc, ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        string NameOf(int? id) => id.HasValue && categories.TryGetValue(id.Value, out var c)
            ? (c.ParentId.HasValue && categories.TryGetValue(c.ParentId.Value, out var parent) ? parent.Name : c.Name)
            : "Sin categoría";
        return Group(items, i => NameOf(i.CategoryId));
    }

    public async Task<IReadOnlyList<GroupTotalDto>> GetByBrandAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var items = await CompletedItemsAsync(fromUtc, toUtc, ct);
        var brands = await db.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return Group(items, i => i.BrandId.HasValue ? brands.GetValueOrDefault(i.BrandId.Value, "Sin marca") : "Sin marca");
    }

    private static IReadOnlyList<GroupTotalDto> Group(IEnumerable<ItemRow> items, Func<ItemRow, string> key) =>
        items.GroupBy(key)
            .Select(g => new GroupTotalDto(g.Key, g.Sum(i => i.Subtotal), g.Sum(i => i.Quantity)))
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

    public async Task<IReadOnlyList<PaymentMethodTotalDto>> GetPaymentMethodsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var payments = await db.SalePayments.AsNoTracking()
            .Where(p => p.Sale.CreatedAt >= fromUtc && p.Sale.CreatedAt < toUtc && p.Sale.Status != SaleStatus.Cancelled)
            .Select(p => new { p.Method, p.Amount, p.SaleId })
            .ToListAsync(ct);
        return payments.GroupBy(p => p.Method)
            .Select(g => new PaymentMethodTotalDto(g.Key, g.Sum(p => p.Amount), g.Select(p => p.SaleId).Distinct().Count()))
            .OrderByDescending(x => x.Total)
            .ToList();
    }

    public async Task<(int ActiveVariants, int LowStock, int Customers)> GetCountsAsync(CancellationToken ct = default)
    {
        var active = db.ProductVariants.Where(v => v.IsActive && v.Product.IsActive);
        return (await active.CountAsync(ct),
                await active.CountAsync(v => v.Stock <= v.MinStock, ct),
                await db.Customers.CountAsync(c => c.IsActive, ct));
    }

    public Task<int> CountExpiringAsync(DateTime untilDate, CancellationToken ct = default) =>
        db.ProductBatches.CountAsync(b => b.Quantity > 0 && b.ExpirationDate != null && b.ExpirationDate <= untilDate && b.Variant.IsActive, ct);
}
