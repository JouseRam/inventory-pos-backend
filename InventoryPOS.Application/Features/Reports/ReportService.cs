using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Reports;

public record SalesTotals(int Count, decimal Total, decimal Discount, decimal Tax, decimal Cost);
public record SalesByDayDto(DateTime Date, decimal Total, int Count);
public record TopProductDto(int VariantId, string Description, decimal TotalQty, decimal TotalRevenue);
public record GroupTotalDto(string Name, decimal TotalRevenue, decimal TotalQty);
public record PaymentMethodTotalDto(string Method, decimal Total, int Count);
public record SalesSummaryDto(DateTime From, DateTime To, int TotalSales, decimal TotalRevenue, decimal TotalDiscount, decimal TotalTax,
    decimal AverageTicket, decimal GrossProfit);
public record DashboardDto(
    decimal SalesToday, int CountToday, decimal SalesWeek, decimal SalesMonth,
    int ProductCount, int LowStockCount, int CustomerCount, int ExpiringCount,
    IReadOnlyList<SaleDto> RecentSales, IReadOnlyList<VariantDto> LowStockProducts);

/// <summary>Read-side aggregations executed in the database; ranges are UTC [from, to).</summary>
public interface IReportQueries
{
    Task<SalesTotals> GetSalesTotalsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<(DateTime CreatedAtUtc, decimal Total)>> GetSaleTotalsByTimeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<GroupTotalDto>> GetByCategoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<GroupTotalDto>> GetByBrandAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentMethodTotalDto>> GetPaymentMethodsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<(int ActiveVariants, int LowStock, int Customers)> GetCountsAsync(CancellationToken ct = default);
    Task<int> CountExpiringAsync(DateTime untilDate, CancellationToken ct = default);
}

/// <summary>Turns local calendar ranges into UTC bounds so "today" means the store's day, not the server's.</summary>
public class ReportService(IReportQueries queries, IUnitOfWork uow, IDateTimeProvider clock, SettingService settings)
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = clock.LocalToday;
        var todayTotals = await TotalsAsync(today, today, ct);
        var week = await TotalsAsync(today.AddDays(-6), today, ct);
        var month = await TotalsAsync(new DateTime(today.Year, today.Month, 1), today, ct);
        var (variants, lowStock, customers) = await queries.GetCountsAsync(ct);
        var warningDays = (int)await settings.GetDecimalAsync(SettingKeys.ExpirationWarningDays, 60, ct);
        var expiring = await queries.CountExpiringAsync(today.AddDays(warningDays), ct);
        var recent = await uow.Sales.GetPagedAsync(null, null, null, null, 1, 10, ct);
        var lowStockVariants = await uow.Variants.GetLowStockAsync(5, ct);

        return new DashboardDto(
            todayTotals.Total, todayTotals.Count, week.Total, month.Total,
            variants, lowStock, customers, expiring,
            recent.Data.Where(s => s.Status != Domain.Constants.SaleStatus.Cancelled).Select(s => s.ToDto(false)).ToList(),
            lowStockVariants.Select(v => v.ToDto()).ToList());
    }

    public async Task<IReadOnlyList<SalesByDayDto>> GetSalesByDayAsync(int days, CancellationToken ct = default)
    {
        var to = clock.LocalToday;
        var from = to.AddDays(-(Math.Clamp(days, 1, 366) - 1));
        var (fromUtc, toUtc) = clock.LocalDaysToUtc(from, to);
        var rows = await queries.GetSaleTotalsByTimeAsync(fromUtc, toUtc, ct);
        return rows.GroupBy(r => clock.ToLocal(r.CreatedAtUtc).Date)
            .Select(g => new SalesByDayDto(g.Key, g.Sum(r => r.Total), g.Count()))
            .OrderBy(d => d.Date).ToList();
    }

    public Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(int days, int limit, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = LastDays(days);
        return queries.GetTopProductsAsync(fromUtc, toUtc, Math.Clamp(limit, 1, 100), ct);
    }

    public Task<IReadOnlyList<GroupTotalDto>> GetByCategoryAsync(int days, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = LastDays(days);
        return queries.GetByCategoryAsync(fromUtc, toUtc, ct);
    }

    public Task<IReadOnlyList<GroupTotalDto>> GetByBrandAsync(int days, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = LastDays(days);
        return queries.GetByBrandAsync(fromUtc, toUtc, ct);
    }

    public Task<IReadOnlyList<PaymentMethodTotalDto>> GetPaymentMethodsAsync(int days, CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = LastDays(days);
        return queries.GetPaymentMethodsAsync(fromUtc, toUtc, ct);
    }

    public async Task<SalesSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var end = (to ?? clock.LocalToday).Date;
        var start = (from ?? end.AddDays(-30)).Date;
        var totals = await TotalsAsync(start, end, ct);
        return new SalesSummaryDto(start, end, totals.Count, totals.Total, totals.Discount, totals.Tax,
            totals.Count > 0 ? Money.Round(totals.Total / totals.Count) : 0, totals.Total - totals.Cost);
    }

    private Task<SalesTotals> TotalsAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct)
    {
        var (fromUtc, toUtc) = clock.LocalDaysToUtc(fromLocal, toLocal);
        return queries.GetSalesTotalsAsync(fromUtc, toUtc, ct);
    }

    private (DateTime FromUtc, DateTime ToUtc) LastDays(int days)
    {
        var to = clock.LocalToday;
        return clock.LocalDaysToUtc(to.AddDays(-(Math.Clamp(days, 1, 366) - 1)), to);
    }
}
