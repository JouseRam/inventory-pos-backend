using InventoryPOS.Application.Features.Reports;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Services;

public class ReportQueries(ApplicationDbContext db) : IReportQueries
{
    private IQueryable<Sale> CompletedSales(DateTime fromUtc, DateTime toUtc) =>
        db.Sales.AsNoTracking().Where(s => s.CreatedAt >= fromUtc && s.CreatedAt < toUtc && s.Status != SaleStatus.Cancelled);

    private IQueryable<SaleItem> CompletedItems(DateTime fromUtc, DateTime toUtc) =>
        db.SaleItems.AsNoTracking().Where(i => i.Sale.CreatedAt >= fromUtc && i.Sale.CreatedAt < toUtc && i.Sale.Status != SaleStatus.Cancelled);

    public async Task<SalesTotals> GetSalesTotalsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var totals = await CompletedSales(fromUtc, toUtc)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(s => s.Total), Discount = g.Sum(s => s.Discount), Tax = g.Sum(s => s.Tax) })
            .FirstOrDefaultAsync(ct);
        var cost = await CompletedItems(fromUtc, toUtc).SumAsync(i => (decimal?)(i.UnitCost * i.Quantity), ct) ?? 0;
        return totals == null
            ? new SalesTotals(0, 0, 0, 0, 0)
            : new SalesTotals(totals.Count, totals.Total, totals.Discount, totals.Tax, Math.Round(cost, 2));
    }

    public async Task<IReadOnlyList<(DateTime CreatedAtUtc, decimal Total)>> GetSaleTotalsByTimeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await CompletedSales(fromUtc, toUtc).Select(s => new { s.CreatedAt, s.Total }).ToListAsync(ct);
        return rows.Select(r => (r.CreatedAt, r.Total)).ToList();
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default) =>
        await CompletedItems(fromUtc, toUtc)
            .GroupBy(i => new { i.VariantId, i.Description })
            .OrderByDescending(g => g.Sum(i => i.Subtotal))
            .Take(limit)
            .Select(g => new TopProductDto(g.Key.VariantId, g.Key.Description, g.Sum(i => i.Quantity), g.Sum(i => i.Subtotal)))
            .ToListAsync(ct);

    /// <summary>Totals per top-level category (subcategories roll up into their parent).</summary>
    public async Task<IReadOnlyList<GroupTotalDto>> GetByCategoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await CompletedItems(fromUtc, toUtc)
            .GroupBy(i => i.Variant.Product.CategoryId)
            .Select(g => new { Id = g.Key, Revenue = g.Sum(i => i.Subtotal), Qty = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        string NameOf(int? id) => id.HasValue && categories.TryGetValue(id.Value, out var c)
            ? (c.ParentId.HasValue && categories.TryGetValue(c.ParentId.Value, out var parent) ? parent.Name : c.Name)
            : "Sin categoría";
        return Merge(rows.Select(r => (NameOf(r.Id), r.Revenue, r.Qty)));
    }

    public async Task<IReadOnlyList<GroupTotalDto>> GetByBrandAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await CompletedItems(fromUtc, toUtc)
            .GroupBy(i => i.Variant.Product.BrandId)
            .Select(g => new { Id = g.Key, Revenue = g.Sum(i => i.Subtotal), Qty = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);
        var brands = await db.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return Merge(rows.Select(r => (r.Id.HasValue ? brands.GetValueOrDefault(r.Id.Value, "Sin marca") : "Sin marca", r.Revenue, r.Qty)));
    }

    private static IReadOnlyList<GroupTotalDto> Merge(IEnumerable<(string Name, decimal Revenue, decimal Qty)> rows) =>
        rows.GroupBy(r => r.Name)
            .Select(g => new GroupTotalDto(g.Key, g.Sum(r => r.Revenue), g.Sum(r => r.Qty)))
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

    public async Task<IReadOnlyList<PaymentMethodTotalDto>> GetPaymentMethodsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await db.SalePayments.AsNoTracking()
            .Where(p => p.Sale.CreatedAt >= fromUtc && p.Sale.CreatedAt < toUtc && p.Sale.Status != SaleStatus.Cancelled)
            .GroupBy(p => p.Method)
            .OrderByDescending(g => g.Sum(p => p.Amount))
            .Select(g => new PaymentMethodTotalDto(g.Key, g.Sum(p => p.Amount), g.Select(p => p.SaleId).Distinct().Count()))
            .ToListAsync(ct);

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
