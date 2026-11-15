using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Catalog;
using InventoryPOS.Application.Features.Orders;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Infrastructure.Services;

namespace InventoryPOS.Application.Tests;

public class PurchaseOrderTests
{
    private sealed class Setup : IDisposable
    {
        public PosTestContext Pos { get; } = new();
        public PurchaseOrderService Orders { get; }
        public int SupplierId { get; private set; }

        public Setup()
        {
            Orders = new PurchaseOrderService(Pos.Uow, new FolioGenerator(Pos.Db), Pos.User, new SettingService(Pos.Uow, new PosGuard(Pos.Env)));
        }

        /// <summary>Two products of one supplier: a dye that sells fast and a shampoo above its minimum that barely sells.</summary>
        public async Task<(int Fast, int Slow, int Low)> CatalogAsync()
        {
            SupplierId = (await new SupplierService(Pos.Uow, new PosGuard(Pos.Env)).CreateAsync(
                new SupplierSaveDto("Distribuidora Bella", "Laura", "55 4120 3311", null, null))).Id;
            async Task<int> Product(string name, string sku, decimal min) => (await Pos.Products.CreateAsync(new ProductCreateDto(name, null, null, null,
                SupplierId, false, [new VariantSaveDto(sku, null, null, null, null, UnitsOfMeasure.Piece, 100, 50, min)]))).Variants.Single().Id;
            var fast = await Product("Tinte rápido", "FAST", 2);
            var slow = await Product("Shampoo", "SLOW", 2);
            var low = await Product("Oxidante", "LOW", 5);
            await Pos.Purchases.ReceiveAsync(new InventoryPOS.Application.Features.Inventory.PurchaseCreateDto(SupplierId, null, null,
                [new(fast, 40, 50, null, null), new(slow, 20, 50, null, null), new(low, 3, 50, null, null)]));
            await Pos.Cash.OpenAsync(new CashOpenDto(0, null));
            // 30 pieces of the dye in the last days: 1 a day, so 10 left last 10 days.
            await Pos.Sales.CreateAsync(PosTestContext.Sale(fast, 30, (PaymentMethods.Cash, 5000)));
            await Pos.Sales.CreateAsync(PosTestContext.Sale(slow, 1, (PaymentMethods.Cash, 500)));
            return (fast, slow, low);
        }

        public void Dispose() => Pos.Dispose();
    }

    [Fact]
    public async Task Suggestions_reorder_what_is_low_or_will_run_out_and_group_it_by_supplier()
    {
        using var s = new Setup();
        var (fast, slow, low) = await s.CatalogAsync();

        var result = await s.Orders.SuggestAsync(days: 30, coverDays: 30);

        var group = Assert.Single(result.Suppliers);
        Assert.Equal(s.SupplierId, group.SupplierId);
        Assert.True(group.HasWhatsApp);
        // Fast: 10 left, sells 1/day → runs out in 10 days; covers 30 days + minimum 2 → 32 − 10 = 22.
        var fastLine = group.Lines.Single(l => l.VariantId == fast);
        Assert.Equal(22, fastLine.SuggestedQuantity);
        Assert.Equal(10, fastLine.DaysLeft);
        // Low: 3 on hand under a minimum of 5, no sales → up to the minimum.
        Assert.Equal(2, group.Lines.Single(l => l.VariantId == low).SuggestedQuantity);
        // Slow: 19 on hand, sold 1 in 30 days → lasts well beyond the period: not suggested.
        Assert.DoesNotContain(group.Lines, l => l.VariantId == slow);
        Assert.Equal(24 * 50m, group.EstimatedTotal);
    }

    [Fact]
    public async Task An_order_goes_from_draft_to_received_and_keeps_its_timeline()
    {
        using var s = new Setup();
        var (fast, _, low) = await s.CatalogAsync();

        var order = await s.Orders.CreateAsync(new OrderSaveDto(s.SupplierId, "Urgente", [new(fast, 22), new(low, 2), new(fast, 3)]));
        Assert.Equal("PED-000001", order.Folio);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(25, order.Items.Single(i => i.VariantId == fast).Quantity); // the same product twice is one line
        Assert.Equal("5541203311", order.WhatsAppPhone);
        Assert.Contains("PED-000001", order.WhatsAppText);
        Assert.Contains("25 × Tinte rápido", order.WhatsAppText);

        // Ordered pieces are not suggested again while they are on their way.
        await s.Orders.MarkSentAsync(order.Id);
        Assert.Empty((await s.Orders.SuggestAsync()).Suppliers);

        order = await s.Orders.SetTrackingAsync(order.Id, new TrackingSaveDto("fedex", "7489 1234 5566"));
        Assert.Equal(PurchaseOrderStatus.Shipped, order.Status);
        Assert.Equal("https://www.fedex.com/fedextrack/?trknbr=7489%201234%205566", order.TrackingUrl);
        order = await s.Orders.AddEventAsync(order.Id, new OrderEventSaveDto("Llegó al centro de distribución de Pachuca"));
        order = await s.Orders.MarkReceivedAsync(order.Id, new OrderReceivedDto(null));

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(["Mercancía recibida.", "Llegó al centro de distribución de Pachuca", "En camino con FedEx, guía 7489 1234 5566.",
            "Pedido enviado al proveedor.", "Pedido creado."], order.Events.Select(e => e.Text));
        Assert.True(order.Events[1].Manual);
        await Assert.ThrowsAsync<DomainException>(() => s.Orders.UpdateAsync(order.Id, new OrderSaveDto(s.SupplierId, null, [new(fast, 1)])));
    }

    [Fact]
    public async Task A_sent_order_can_still_be_adjusted_and_records_the_change()
    {
        using var s = new Setup();
        var (fast, _, _) = await s.CatalogAsync();
        var order = await s.Orders.CreateAsync(new OrderSaveDto(s.SupplierId, null, [new(fast, 10)]));
        await s.Orders.MarkSentAsync(order.Id);

        order = await s.Orders.UpdateAsync(order.Id, new OrderSaveDto(s.SupplierId, null, [new(fast, 6, 48)]));

        var item = Assert.Single(order.Items);
        Assert.Equal(6, item.Quantity);
        Assert.Equal(48, item.UnitCost);
        Assert.Single(order.Events, e => e.Text.Contains("modificó"));

        // Saving the same list again (as sending or receiving does) adds nothing to the timeline.
        order = await s.Orders.UpdateAsync(order.Id, new OrderSaveDto(s.SupplierId, "Sin cambios en la lista", [new(fast, 6, 48)]));
        Assert.Single(order.Events, e => e.Text.Contains("modificó"));
    }

    [Fact]
    public void Carriers_without_a_public_link_use_the_universal_tracker()
    {
        Assert.Equal("https://t.17track.net/es#nums=EST123", Carriers.TrackingUrl("estafeta", "EST123"));
        Assert.Null(Carriers.TrackingUrl("nadie", "123"));
    }
}
