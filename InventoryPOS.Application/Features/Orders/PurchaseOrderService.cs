using FluentValidation;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Orders;

/// <summary>A presentation the system suggests reordering and why.</summary>
public record SuggestionLineDto(int VariantId, string Description, string? Sku, decimal Stock, decimal MinStock, decimal SoldInPeriod,
    decimal DailyRate, int? DaysLeft, decimal SuggestedQuantity, decimal UnitCost, string Reason);

/// <summary>Suggestions grouped by the supplier who sells those products (null: products without supplier).</summary>
public record SupplierSuggestionDto(int? SupplierId, string SupplierName, bool HasWhatsApp, decimal EstimatedTotal, IReadOnlyList<SuggestionLineDto> Lines);

public record SuggestionsDto(int Days, int CoverDays, IReadOnlyList<SupplierSuggestionDto> Suppliers);

public record OrderItemSaveDto(int VariantId, decimal Quantity, decimal? UnitCost = null);

public record OrderSaveDto(int? SupplierId, string? Notes, List<OrderItemSaveDto> Items);

public record OrderItemDto(int Id, int VariantId, string Description, string? Sku, decimal Quantity, decimal UnitCost, decimal Subtotal, decimal Stock,
    bool TracksBatches = false, decimal ReceivedQuantity = 0)
{
    /// <summary>Pieces still expected from the supplier.</summary>
    public decimal Pending => Math.Max(0, Quantity - ReceivedQuantity);
}

/// <summary>A reception that brought goods of the order in; its labels can be printed again from the order.</summary>
public record OrderReceptionDto(int Id, string Folio, DateTime At, decimal Pieces, decimal Total);

public record OrderEventDto(DateTime At, string Text, bool Manual);

/// <summary>WhatsAppPhone is the supplier's number when it can receive WhatsApp; TrackingUrl opens the carrier's tracking page.</summary>
public record OrderDto(int Id, string Folio, int? SupplierId, string? SupplierName, string? SupplierContact, string? WhatsAppPhone,
    string Status, string? Notes, DateTime CreatedAt, DateTime? SentAt, string? Carrier, string? CarrierName, string? TrackingNumber,
    string? TrackingUrl, DateTime? ShippedAt, DateTime? ReceivedAt, int? PurchaseId, decimal EstimatedTotal, string WhatsAppText,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<OrderEventDto> Events, IReadOnlyList<OrderReceptionDto>? Receptions = null);

public record OrderSummaryDto(int Id, string Folio, string? SupplierName, string Status, DateTime CreatedAt, int ItemCount, decimal EstimatedTotal,
    string? CarrierName, string? TrackingNumber);

public record TrackingSaveDto(string Carrier, string TrackingNumber);

public record OrderEventSaveDto(string Text);

/// <summary>A reception for the order. With missing pieces the order stays open for the rest, unless <paramref name="CloseIncomplete"/>.</summary>
public record OrderReceivedDto(int? PurchaseId, bool CloseIncomplete = false);

public record CarrierDto(string Code, string Name);

public class OrderSaveValidator : AbstractValidator<OrderSaveDto>
{
    public OrderSaveValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Agrega al menos un producto al pedido.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            i.RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost.HasValue);
        });
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class TrackingSaveValidator : AbstractValidator<TrackingSaveDto>
{
    public TrackingSaveValidator()
    {
        RuleFor(x => x.Carrier).Must(c => Carriers.Find(c) != null).WithMessage("Elige la paquetería.");
        RuleFor(x => x.TrackingNumber).NotEmpty().WithMessage("Captura el número de guía.").MaximumLength(60);
    }
}

/// <summary>
/// Carriers common in Mexico. Those with a stable public link open their own tracking page with the guide number;
/// the rest open 17TRACK, a free tracker that covers them all.
/// </summary>
public static class Carriers
{
    private const string UniversalTracker = "https://t.17track.net/es#nums={0}";

    private static readonly (string Code, string Name, string Url)[] List =
    [
        ("estafeta", "Estafeta", UniversalTracker),
        ("dhl", "DHL", "https://www.dhl.com/mx-es/home/rastreo.html?tracking-id={0}&submit=1"),
        ("fedex", "FedEx", "https://www.fedex.com/fedextrack/?trknbr={0}"),
        ("ups", "UPS", "https://www.ups.com/track?tracknum={0}"),
        ("paquetexpress", "Paquetexpress", UniversalTracker),
        ("redpack", "Redpack", UniversalTracker),
        ("jt", "J&T Express", UniversalTracker),
        ("99minutos", "99 Minutos", UniversalTracker),
        ("correos", "Correos de México", UniversalTracker),
        ("otra", "Otra", UniversalTracker),
    ];

    public static IReadOnlyList<CarrierDto> All { get; } = List.Select(c => new CarrierDto(c.Code, c.Name)).ToList();

    public static (string Code, string Name, string Url)? Find(string? code) =>
        List.Cast<(string Code, string Name, string Url)?>().FirstOrDefault(c => c!.Value.Code == code);

    public static string? TrackingUrl(string? code, string? number) =>
        Find(code) is { } c && !string.IsNullOrWhiteSpace(number) ? string.Format(c.Url, Uri.EscapeDataString(number.Trim())) : null;
}

/// <summary>
/// Orders to suppliers. Suggestions come from stock and sales: what is at or under its minimum, or will run out before
/// the coverage period at the pace it sold, ordered up to that period's need plus the minimum, per supplier.
/// </summary>
public class PurchaseOrderService(IUnitOfWork uow, IFolioGenerator folios, ICurrentUser currentUser, SettingService settings)
{
    private IRepository<PurchaseOrder> Orders => uow.Repository<PurchaseOrder>();

    public async Task<SuggestionsDto> SuggestAsync(int days = 30, int coverDays = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 365);
        coverDays = Math.Clamp(coverDays, 7, 180);
        var since = DateTime.UtcNow.AddDays(-days);
        var products = await uow.Products.SearchAsync(null, null, null, false, ct);
        var sold = (await uow.Repository<SaleItem>().ListAsync(i => i.Sale.CreatedAt >= since && i.Sale.Status == SaleStatus.Completed, ct))
            .GroupBy(i => i.VariantId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        // Pieces already ordered and on their way are not suggested again.
        var onOrder = (await uow.Repository<PurchaseOrderItem>().ListAsync(i =>
                i.PurchaseOrder.Status == PurchaseOrderStatus.Sent || i.PurchaseOrder.Status == PurchaseOrderStatus.Shipped
                || i.PurchaseOrder.Status == PurchaseOrderStatus.PartiallyReceived, ct))
            .GroupBy(i => i.VariantId).ToDictionary(g => g.Key, g => g.Sum(i => Math.Max(0, i.Quantity - i.ReceivedQuantity)));

        var groups = new Dictionary<int, (string Name, bool WhatsApp, List<SuggestionLineDto> Lines)>();
        foreach (var product in products)
        foreach (var v in product.Variants.Where(v => v.IsActive))
        {
            var soldQty = sold.GetValueOrDefault(v.Id);
            var daily = soldQty / days;
            var available = v.Stock + onOrder.GetValueOrDefault(v.Id);
            int? daysLeft = daily > 0 ? (int)Math.Floor(available / daily) : null;
            var target = Math.Ceiling(daily * coverDays) + v.MinStock;
            var low = available <= v.MinStock;
            var runningOut = daysLeft.HasValue && daysLeft < coverDays;
            var quantity = Math.Ceiling(target - available);
            if ((!low && !runningOut) || quantity <= 0) continue;

            var reason = low && v.Stock <= v.MinStock ? $"En su mínimo ({v.MinStock:0.##}) o debajo"
                : low ? "Con lo ya pedido apenas llega al mínimo"
                : $"Al ritmo de venta se agota en {daysLeft} días";
            var key = product.SupplierId ?? 0;
            if (!groups.TryGetValue(key, out var group))
                groups[key] = group = (product.Supplier?.Name ?? "Sin proveedor asignado", PhoneNumber.Normalize(product.Supplier?.Phone) != null, []);
            group.Lines.Add(new SuggestionLineDto(v.Id, v.DisplayName(), v.SKU, v.Stock, v.MinStock, soldQty, Math.Round(daily, 2),
                daysLeft, quantity, v.Cost, reason));
        }

        return new SuggestionsDto(days, coverDays, groups
            .OrderBy(g => g.Key == 0).ThenBy(g => g.Value.Name)
            .Select(g => new SupplierSuggestionDto(g.Key == 0 ? null : g.Key, g.Value.Name, g.Value.WhatsApp,
                Money.Round(g.Value.Lines.Sum(l => l.SuggestedQuantity * l.UnitCost)),
                g.Value.Lines.OrderBy(l => l.DaysLeft ?? int.MaxValue).ThenBy(l => l.Description).ToList()))
            .ToList());
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var orders = await Orders.ListAsync(o => status == null || o.Status == status, ct);
        var suppliers = (await uow.Repository<Supplier>().ListAsync(null, ct)).ToDictionary(s => s.Id, s => s.Name);
        var items = (await uow.Repository<PurchaseOrderItem>().ListAsync(null, ct)).GroupBy(i => i.PurchaseOrderId).ToDictionary(g => g.Key, g => g.ToList());
        return orders.OrderByDescending(o => o.CreatedAt).Select(o =>
        {
            var lines = items.GetValueOrDefault(o.Id) ?? [];
            return new OrderSummaryDto(o.Id, o.Folio, o.SupplierId is { } s ? suppliers.GetValueOrDefault(s) : null, o.Status, o.CreatedAt,
                lines.Count, Money.Round(lines.Sum(l => l.Quantity * l.UnitCost)), Carriers.Find(o.Carrier)?.Name, o.TrackingNumber);
        }).ToList();
    }

    public async Task<OrderDto> GetAsync(int id, CancellationToken ct = default)
    {
        var o = await Orders.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Pedido", id);
        var supplier = o.SupplierId is { } sid ? await uow.Repository<Supplier>().GetByIdAsync(sid, ct) : null;
        var items = (await uow.Repository<PurchaseOrderItem>().ListAsync(i => i.PurchaseOrderId == id, ct)).OrderBy(i => i.Id).ToList();
        var stock = (await uow.Variants.GetByIdsWithProductAsync(items.Select(i => i.VariantId).Distinct(), ct)).ToDictionary(v => v.Id);
        var events = await uow.Repository<PurchaseOrderEvent>().ListAsync(e => e.PurchaseOrderId == id, ct);
        var itemDtos = items.Select(i => new OrderItemDto(i.Id, i.VariantId, i.Description, stock.GetValueOrDefault(i.VariantId)?.SKU,
            i.Quantity, i.UnitCost, Money.Round(i.Quantity * i.UnitCost), stock.GetValueOrDefault(i.VariantId)?.Stock ?? 0,
            stock.GetValueOrDefault(i.VariantId)?.Product.TracksBatches ?? false, i.ReceivedQuantity)).ToList();
        var receptions = (await uow.Repository<Purchase>().ListAsync(p => p.PurchaseOrderId == id, ct)).OrderBy(p => p.CreatedAt).ToList();
        var receivedPieces = receptions.Count == 0 ? new Dictionary<int, decimal>()
            : (await uow.Repository<PurchaseItem>().ListAsync(i => i.Purchase.PurchaseOrderId == id, ct))
                .GroupBy(i => i.PurchaseId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        return new OrderDto(o.Id, o.Folio, o.SupplierId, supplier?.Name, supplier?.ContactName, PhoneNumber.Normalize(supplier?.Phone),
            o.Status, o.Notes, o.CreatedAt, o.SentAt, o.Carrier, Carriers.Find(o.Carrier)?.Name, o.TrackingNumber,
            Carriers.TrackingUrl(o.Carrier, o.TrackingNumber), o.ShippedAt, o.ReceivedAt, o.PurchaseId,
            Money.Round(itemDtos.Sum(i => i.Subtotal)), await WhatsAppTextAsync(o, supplier, itemDtos, ct), itemDtos,
            events.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).Select(e => new OrderEventDto(e.CreatedAt, e.Text, e.Manual)).ToList(),
            receptions.Select(p => new OrderReceptionDto(p.Id, p.Folio, p.CreatedAt, receivedPieces.GetValueOrDefault(p.Id), p.Total)).ToList());
    }

    public async Task<OrderDto> CreateAsync(OrderSaveDto dto, CancellationToken ct = default)
    {
        await EnsureSupplierAsync(dto.SupplierId, ct);
        var id = await uow.ExecuteInTransactionAsync(async () =>
        {
            var order = new PurchaseOrder
            {
                Folio = await folios.NextAsync(FolioSeries.PurchaseOrder, ct), SupplierId = dto.SupplierId,
                Notes = dto.Notes?.Trim(), UserId = currentUser.UserId,
            };
            await SetItemsAsync(order, dto.Items, ct);
            Orders.Add(order);
            AddEvent(order, "Pedido creado.");
            await uow.SaveChangesAsync(ct);
            return order.Id;
        }, ct);
        return await GetAsync(id, ct);
    }

    /// <summary>The list stays editable until the goods arrive (suppliers often adjust quantities after confirming).</summary>
    public async Task<OrderDto> UpdateAsync(int id, OrderSaveDto dto, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
            throw new DomainException("Este pedido ya está cerrado; no se puede modificar.");
        await EnsureSupplierAsync(dto.SupplierId, ct);
        var items = uow.Repository<PurchaseOrderItem>();
        var old = await items.ListAsync(i => i.PurchaseOrderId == id, ct);
        static string Key(IEnumerable<(int VariantId, decimal Quantity, decimal UnitCost)> lines) =>
            string.Join(";", lines.OrderBy(l => l.VariantId).Select(l => $"{l.VariantId}:{l.Quantity:0.###}:{l.UnitCost:0.##}"));
        var before = Key(old.Select(i => (i.VariantId, i.Quantity, i.UnitCost))) + "|" + order.SupplierId;
        foreach (var item in old) items.Remove(item);
        order.Items.Clear();
        order.SupplierId = dto.SupplierId;
        order.Notes = dto.Notes?.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        await SetItemsAsync(order, dto.Items, ct);
        // What already arrived stays counted when the list is adjusted for the rest.
        foreach (var item in order.Items) item.ReceivedQuantity = old.Where(o => o.VariantId == item.VariantId).Sum(o => o.ReceivedQuantity);
        var after = Key(order.Items.Select(i => (i.VariantId, i.Quantity, i.UnitCost))) + "|" + order.SupplierId;
        // Saving before sending or receiving is routine; only a real change goes to the timeline.
        if (order.Status != PurchaseOrderStatus.Draft && before != after) AddEvent(order, "Se modificó la lista del pedido.");
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> MarkSentAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status != PurchaseOrderStatus.Draft) throw new DomainException("El pedido ya fue enviado.");
        order.Status = PurchaseOrderStatus.Sent;
        order.SentAt = DateTime.UtcNow;
        AddEvent(order, "Pedido enviado al proveedor.");
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<OrderDto> SetTrackingAsync(int id, TrackingSaveDto dto, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
            throw new DomainException("Este pedido ya está cerrado.");
        var carrier = Carriers.Find(dto.Carrier)!.Value;
        order.Carrier = carrier.Code;
        order.TrackingNumber = dto.TrackingNumber.Trim();
        order.ShippedAt ??= DateTime.UtcNow;
        order.SentAt ??= order.ShippedAt;
        // The rest of a partial delivery is on its way: the order keeps saying part of it already arrived.
        if (order.Status != PurchaseOrderStatus.PartiallyReceived) order.Status = PurchaseOrderStatus.Shipped;
        AddEvent(order, $"En camino con {carrier.Name}, guía {order.TrackingNumber}.");
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>A movement the user reads on the carrier's site or hears from the supplier.</summary>
    public async Task<OrderDto> AddEventAsync(int id, OrderEventSaveDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) throw new DomainException("Escribe el movimiento.");
        var order = await LoadAsync(id, ct);
        AddEvent(order, dto.Text.Trim()[..Math.Min(dto.Text.Trim().Length, 300)], manual: true);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Compares a reception with the order: what arrived complete, what is missing, what came extra. With missing pieces the order
    /// stays "Recibido parcial" for the rest (a later reception completes it), unless the user closes it with the shortage.
    /// </summary>
    public async Task<OrderDto> MarkReceivedAsync(int id, OrderReceivedDto dto, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled) throw new DomainException("Este pedido ya está cerrado.");
        var items = (await uow.Repository<PurchaseOrderItem>().ListAsync(i => i.PurchaseOrderId == id, ct)).ToList();
        var now = DateTime.UtcNow;

        if (dto.PurchaseId is not { } purchaseId)
        {
            // Marked by hand, without a reception: everything is taken as arrived.
            foreach (var item in items) item.ReceivedQuantity = item.Quantity;
            Close(order, now);
            AddEvent(order, "Mercancía recibida.");
            await uow.SaveChangesAsync(ct);
            return await GetAsync(id, ct);
        }

        var purchase = await uow.Purchases.GetWithItemsAsync(purchaseId, ct) ?? throw NotFoundException.For("Recepción", purchaseId);
        if (purchase.PurchaseOrderId is { } linked)
            throw new DomainException(linked == id ? $"La recepción {purchase.Folio} ya se contó en este pedido." : $"La recepción {purchase.Folio} ya es de otro pedido.");
        purchase.PurchaseOrderId = id;
        order.PurchaseId = purchase.Id;

        var arrived = purchase.Items.GroupBy(i => i.VariantId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var extra = new List<string>();
        foreach (var (variantId, quantity) in arrived)
        {
            var line = items.FirstOrDefault(i => i.VariantId == variantId);
            if (line == null)
            {
                extra.Add($"{quantity:0.##} × {purchase.Items.First(i => i.VariantId == variantId).Variant.DisplayName()} (no venía en el pedido)");
                continue;
            }
            var before = Math.Max(0, line.Quantity - line.ReceivedQuantity);
            line.ReceivedQuantity += quantity;
            if (quantity > before) extra.Add($"{quantity - before:0.##} × {line.Description} de más");
        }
        var missing = items.Where(i => i.ReceivedQuantity < i.Quantity).ToList();
        var shortage = string.Join("; ", missing.Select(i => $"{i.Quantity - i.ReceivedQuantity:0.##} × {i.Description}"));

        var text = missing.Count == 0
            ? $"Recepción {purchase.Folio}: llegó completo."
            : $"Recepción {purchase.Folio}: llegó incompleto. Falta: {shortage}.";
        if (extra.Count > 0) text += $" Llegó además: {string.Join("; ", extra)}.";
        AddEvent(order, text[..Math.Min(text.Length, 1000)]);

        if (missing.Count == 0) Close(order, now);
        else if (dto.CloseIncomplete)
        {
            Close(order, now);
            AddEvent(order, "Pedido cerrado con faltante: ya no se espera el resto.");
        }
        else order.Status = PurchaseOrderStatus.PartiallyReceived;

        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Stops waiting for what is missing of a partial delivery.</summary>
    public async Task<OrderDto> CloseIncompleteAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status != PurchaseOrderStatus.PartiallyReceived) throw new DomainException("Solo se cierra así un pedido recibido en parte.");
        Close(order, DateTime.UtcNow);
        AddEvent(order, "Pedido cerrado con faltante: ya no se espera el resto.");
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void Close(PurchaseOrder order, DateTime at)
    {
        order.Status = PurchaseOrderStatus.Received;
        order.ReceivedAt = at;
    }

    public async Task<OrderDto> CancelAsync(int id, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, ct);
        if (order.Status == PurchaseOrderStatus.Received) throw new DomainException("Un pedido recibido no se cancela.");
        if (order.Status == PurchaseOrderStatus.PartiallyReceived)
            throw new DomainException("Parte del pedido ya llegó: ciérralo con faltante en lugar de cancelarlo.");
        order.Status = PurchaseOrderStatus.Cancelled;
        AddEvent(order, "Pedido cancelado.");
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<PurchaseOrder> LoadAsync(int id, CancellationToken ct) =>
        await Orders.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Pedido", id);

    private async Task EnsureSupplierAsync(int? supplierId, CancellationToken ct)
    {
        if (supplierId.HasValue && !await uow.Repository<Supplier>().AnyAsync(s => s.Id == supplierId, ct))
            throw NotFoundException.For("Proveedor", supplierId.Value);
    }

    private async Task SetItemsAsync(PurchaseOrder order, IEnumerable<OrderItemSaveDto> items, CancellationToken ct)
    {
        var merged = items.GroupBy(i => i.VariantId).Select(g => (VariantId: g.Key, Quantity: g.Sum(i => i.Quantity), g.Last().UnitCost)).ToList();
        var variants = (await uow.Variants.GetByIdsWithProductAsync(merged.Select(i => i.VariantId), ct)).ToDictionary(v => v.Id);
        foreach (var i in merged)
        {
            if (!variants.TryGetValue(i.VariantId, out var v)) throw NotFoundException.For("Presentación", i.VariantId);
            order.Items.Add(new PurchaseOrderItem
            {
                VariantId = v.Id, Description = v.DisplayName(), Quantity = i.Quantity, UnitCost = Money.Round(i.UnitCost ?? v.Cost),
            });
        }
    }

    private void AddEvent(PurchaseOrder order, string text, bool manual = false) =>
        order.Events.Add(new PurchaseOrderEvent { Text = text, Manual = manual, UserId = currentUser.UserId });

    /// <summary>The order as a WhatsApp message to the supplier: one line per product with its code and quantity.</summary>
    private async Task<string> WhatsAppTextAsync(PurchaseOrder o, Supplier? supplier, IReadOnlyList<OrderItemDto> items, CancellationToken ct)
    {
        var business = (await settings.GetAsync(SettingKeys.BusinessName, ct))?.Trim();
        var greeting = string.IsNullOrWhiteSpace(supplier?.ContactName) ? "Hola" : $"Hola {supplier.ContactName.Trim()}";
        var lines = items.Select(i => $"• {i.Quantity:0.##} × {i.Description}{(string.IsNullOrEmpty(i.Sku) ? "" : $" ({i.Sku})")}");
        return $"{greeting}, {(string.IsNullOrEmpty(business) ? "le" : $"de parte de {business} le")} comparto el pedido *{o.Folio}*:\n\n"
            + string.Join("\n", lines)
            + (string.IsNullOrWhiteSpace(o.Notes) ? "" : $"\n\nNotas: {o.Notes}")
            + "\n\n¿Me confirma existencias, total y fecha de envío? Gracias.";
    }
}
