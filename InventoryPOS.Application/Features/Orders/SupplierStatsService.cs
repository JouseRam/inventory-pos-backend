using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Orders;

/// <summary>How much was bought from one supplier in the period, and how often: the numbers to negotiate discounts or gifts.</summary>
public record SupplierStatsRowDto(int? SupplierId, string SupplierName, decimal Total, decimal SharePercent, int Receptions, decimal Pieces,
    int? EveryDays, DateTime? FirstPurchase, DateTime? LastPurchase, decimal MonthlyAverage, int Orders, decimal? DeliveryDays);

public record SupplierStatsDto(DateTime From, DateTime To, int Days, decimal Total, IReadOnlyList<SupplierStatsRowDto> Suppliers);

public record SupplierMonthDto(int Year, int Month, decimal Total, int Receptions);

/// <summary>One product bought from the supplier: pieces, money and how its cost moved within the period.</summary>
public record SupplierProductDto(int VariantId, string Description, string? Sku, decimal Pieces, decimal Total, int Times,
    decimal FirstCost, decimal LastCost, decimal CostChangePercent);

public record SupplierStatsDetailDto(SupplierStatsRowDto Summary, string? ContactName, string? WhatsAppPhone,
    IReadOnlyList<SupplierMonthDto> Months, IReadOnlyList<SupplierProductDto> Products, string NegotiationText);

public class SupplierStatsService(IUnitOfWork uow, IDateTimeProvider clock, SettingService settings)
{
    private static readonly System.Globalization.CultureInfo Mx = System.Globalization.CultureInfo.GetCultureInfo("es-MX");

    private sealed record Line(int? SupplierId, DateTime At, int VariantId, decimal Quantity, decimal UnitCost, decimal Subtotal);

    public async Task<SupplierStatsDto> GetAsync(int days = 365, CancellationToken ct = default)
    {
        var (from, to, lines) = await LinesAsync(days, ct);
        var rows = await RowsAsync(lines, from, to, ct);
        return new SupplierStatsDto(from, to, (to - from).Days + 1, Money.Round(lines.Sum(l => l.Subtotal)), rows);
    }

    public async Task<SupplierStatsDetailDto> GetSupplierAsync(int supplierId, int days = 365, CancellationToken ct = default)
    {
        var supplier = await uow.Repository<Supplier>().GetByIdAsync(supplierId, ct) ?? throw NotFoundException.For("Proveedor", supplierId);
        var (from, to, all) = await LinesAsync(days, ct);
        var summary = (await RowsAsync(all, from, to, ct)).FirstOrDefault(r => r.SupplierId == supplierId)
            ?? new SupplierStatsRowDto(supplierId, supplier.Name, 0, 0, 0, 0, null, null, null, 0, 0, null);
        var lines = all.Where(l => l.SupplierId == supplierId).ToList();

        // Every month of the period, also the ones without purchases, so the trend reads right.
        var months = new List<SupplierMonthDto>();
        for (var m = new DateTime(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            var inMonth = lines.Where(l => l.At.Year == m.Year && l.At.Month == m.Month).ToList();
            months.Add(new SupplierMonthDto(m.Year, m.Month, Money.Round(inMonth.Sum(l => l.Subtotal)), inMonth.Select(l => l.At).Distinct().Count()));
        }

        var variants = (await uow.Variants.GetByIdsWithProductAsync(lines.Select(l => l.VariantId).Distinct(), ct)).ToDictionary(v => v.Id);
        var products = lines.GroupBy(l => l.VariantId).Select(g =>
        {
            var ordered = g.OrderBy(l => l.At).ToList();
            var first = ordered[0].UnitCost;
            var last = ordered[^1].UnitCost;
            var v = variants.GetValueOrDefault(g.Key);
            return new SupplierProductDto(g.Key, v?.DisplayName() ?? $"Presentación {g.Key}", v?.SKU, g.Sum(l => l.Quantity),
                Money.Round(g.Sum(l => l.Subtotal)), ordered.Select(l => l.At).Distinct().Count(), first, last,
                first > 0 ? Math.Round((last - first) / first * 100, 1) : 0);
        }).OrderByDescending(p => p.Total).ToList();

        var business = (await settings.GetAsync(SettingKeys.BusinessName, ct))?.Trim() is { Length: > 0 } b ? b : "nuestra tienda";
        var phone = PhoneNumber.Normalize(supplier.Phone);
        return new SupplierStatsDetailDto(summary, supplier.ContactName, phone, months, products,
            NegotiationText(summary, supplier.ContactName, business, products, (to - from).Days + 1));
    }

    private async Task<(DateTime From, DateTime To, List<Line> Lines)> LinesAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 30, 730);
        var to = clock.LocalToday;
        var from = to.AddDays(-(days - 1));
        var (fromUtc, toUtc) = clock.LocalDaysToUtc(from, to);
        var purchases = (await uow.Repository<Purchase>().ListAsync(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtc, ct)).ToDictionary(p => p.Id);
        var items = await uow.Repository<PurchaseItem>().ListAsync(i => i.Purchase.CreatedAt >= fromUtc && i.Purchase.CreatedAt < toUtc, ct);
        var lines = items.Where(i => purchases.ContainsKey(i.PurchaseId)).Select(i =>
        {
            var p = purchases[i.PurchaseId];
            return new Line(p.SupplierId, clock.ToLocal(p.CreatedAt), i.VariantId, i.Quantity, i.UnitCost, i.Subtotal);
        }).ToList();
        return (from, to, lines);
    }

    private async Task<List<SupplierStatsRowDto>> RowsAsync(List<Line> lines, DateTime from, DateTime to, CancellationToken ct)
    {
        var suppliers = (await uow.Repository<Supplier>().ListAsync(null, ct)).ToDictionary(s => s.Id);
        var (fromUtc, toUtc) = clock.LocalDaysToUtc(from, to);
        var orders = await uow.Repository<PurchaseOrder>().ListAsync(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
            && o.Status != PurchaseOrderStatus.Cancelled && o.Status != PurchaseOrderStatus.Draft, ct);
        var grand = lines.Sum(l => l.Subtotal);

        return lines.GroupBy(l => l.SupplierId).Select(g =>
        {
            // A reception is a purchase on a given moment; several lines of the same one count once.
            var dates = g.Select(l => l.At).Distinct().OrderBy(d => d).ToList();
            // Average days between purchases; only meaningful when they are spread over more than a day.
            var span = (dates[^1] - dates[0]).TotalDays;
            int? every = dates.Count > 1 && span >= 1 ? Math.Max(1, (int)Math.Round(span / (dates.Count - 1))) : null;
            // Per month since the first purchase in the period, so a new supplier is not diluted over the whole year.
            var months = Math.Max(1m, (decimal)(to.Date.AddDays(1) - dates[0].Date).TotalDays / 30.4m);
            var mine = orders.Where(o => o.SupplierId == g.Key).ToList();
            var delivered = mine.Where(o => o.ReceivedAt.HasValue && o.SentAt.HasValue).Select(o => (decimal)(o.ReceivedAt!.Value - o.SentAt!.Value).TotalDays).ToList();
            var total = g.Sum(l => l.Subtotal);
            return new SupplierStatsRowDto(g.Key,
                g.Key is int id && suppliers.TryGetValue(id, out var s) ? s.Name : "Sin proveedor",
                Money.Round(total), grand > 0 ? Math.Round(total / grand * 100, 1) : 0, dates.Count, g.Sum(l => l.Quantity), every,
                dates[0], dates[^1], Money.Round(total / months), mine.Count, delivered.Count > 0 ? Math.Round(delivered.Average(), 1) : null);
        }).OrderByDescending(r => r.Total).ToList();
    }

    private static string NegotiationText(SupplierStatsRowDto s, string? contact, string business, IReadOnlyList<SupplierProductDto> products, int days)
    {
        var period = days >= 360 ? "el último año" : $"los últimos {days} días";
        var top = string.Join(", ", products.Take(3).Select(p => $"{p.Description} ({p.Pieces.ToString("0.##", Mx)} pzas)"));
        var text = $"Hola{(string.IsNullOrWhiteSpace(contact) ? "" : " " + contact.Trim())}, le escribo de parte de {business}. "
            + $"En {period} le hemos comprado ${s.Total.ToString("N2", Mx)} en {s.Receptions} {(s.Receptions == 1 ? "compra" : "compras")}";
        if (s.EveryDays is int every) text += $", en promedio cada {every} días";
        text += ".";
        if (top.Length > 0) text += $" Lo que más le compramos: {top}.";
        text += " Queremos seguir trabajando con ustedes y crecer los pedidos: ¿nos podría ofrecer un descuento por volumen, "
            + "mejores precios en estos productos o producto de regalo en nuestros próximos pedidos? Gracias.";
        return text;
    }
}
