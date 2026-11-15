using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Alerts;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Domain.Constants;

namespace InventoryPOS.Application.Tests;

public class StockAlertTests
{
    private static async Task<int> ProductAsync(PosTestContext pos, decimal stock, decimal min)
    {
        var id = (await pos.Products.CreateAsync(new ProductCreateDto("Tinte 7.1", null, null, null, null, false,
            [new VariantSaveDto("T71", null, null, null, null, UnitsOfMeasure.Piece, 189, 90, min)]))).Variants.Single().Id;
        await pos.Purchases.ReceiveAsync(new PurchaseCreateDto(null, null, null, [new(id, stock, 90, null, null)]));
        return id;
    }

    [Fact]
    public async Task The_administrator_is_alerted_when_a_product_reaches_its_minimum_and_the_alert_closes_when_restocked()
    {
        using var pos = new PosTestContext();
        pos.User.IsSuperAdmin = true;
        var alerts = new StockAlertService(pos.Uow, pos.User);
        var id = await ProductAsync(pos, 5, 3);
        Assert.Empty((await alerts.GetAsync()).Alerts);

        await pos.Inventory.AdjustAsync(new AdjustmentDto(id, -2, "Merma", null, null)); // 3 left: at the minimum
        var result = await alerts.GetAsync();
        var alert = Assert.Single(result.Alerts);
        Assert.Equal(1, result.Unread);
        Assert.Equal((3m, 3m, false), (alert.Stock, alert.MinStock, alert.OutOfStock));

        // Reading it again does not duplicate it; once read it stays listed but stops counting.
        await alerts.MarkAllReadAsync();
        result = await alerts.GetAsync();
        Assert.Single(result.Alerts);
        Assert.Equal(0, result.Unread);

        // Running out brings it back as unread.
        await pos.Inventory.AdjustAsync(new AdjustmentDto(id, -3, "Merma", null, null));
        result = await alerts.GetAsync();
        Assert.Equal(1, result.Unread);
        Assert.True(result.Alerts.Single().OutOfStock);

        // Restocked above the minimum: the alert closes by itself.
        await pos.Purchases.ReceiveAsync(new PurchaseCreateDto(null, null, null, [new(id, 10, 90, null, null)]));
        Assert.Empty((await alerts.GetAsync()).Alerts);
    }

    [Fact]
    public async Task Only_the_administrator_gets_stock_alerts()
    {
        using var pos = new PosTestContext();
        await Assert.ThrowsAsync<ForbiddenException>(() => new StockAlertService(pos.Uow, pos.User).GetAsync());
    }
}
