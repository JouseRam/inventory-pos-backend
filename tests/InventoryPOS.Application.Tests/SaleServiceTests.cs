using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

public class SaleServiceTests
{
    [Fact]
    public async Task Sale_requires_an_open_cash_session()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5));

        var ex = await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 200))));
        Assert.Contains("caja", ex.Message);
    }

    [Fact]
    public async Task Sale_consumes_earliest_expiring_lot_first_and_writes_kardex()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("LATE", "2028-06-30", 5), ("SOON", "2027-01-31", 3));
        await ctx.Cash.OpenAsync(new CashOpenDto(0, null));

        var sale = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 4, ("Efectivo", 800)));

        Assert.Equal("V-000001", sale.Folio);
        Assert.Equal(4, await ctx.StockOfAsync(dye.Id));
        var lots = await ctx.LotsOfAsync(dye.Id);
        Assert.Equal(0, lots["SOON"]);
        Assert.Equal(4, lots["LATE"]);
        var saleMovements = await ctx.Db.InventoryMovements.Where(m => m.Reference == sale.Folio).ToListAsync();
        Assert.Equal(2, saleMovements.Count);
        Assert.Equal(-4, saleMovements.Sum(m => m.Quantity));
    }

    [Fact]
    public async Task Mixed_payment_applies_card_exactly_and_gives_change_from_cash()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5));
        await ctx.Cash.OpenAsync(new CashOpenDto(100, null));

        var sale = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 2, ("Tarjeta", 150), ("Efectivo", 300)));

        Assert.Equal(400, sale.Total);
        Assert.Equal(450, sale.AmountPaid);
        Assert.Equal(50, sale.Change);
        Assert.Equal(55.17m, sale.Tax);
        Assert.Equal("Mixto", sale.PaymentMethod);
        Assert.Equal(250, sale.Payments!.Single(p => p.Method == PaymentMethods.Cash).Amount);
        Assert.Equal(sale.Total, sale.Payments!.Sum(p => p.Amount));
    }

    [Fact]
    public async Task Card_payments_cannot_exceed_the_total()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5));
        await ctx.Cash.OpenAsync(new CashOpenDto(0, null));

        await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Tarjeta", 250))));
    }

    [Fact]
    public async Task Failed_sale_rolls_back_stock_and_does_not_burn_a_folio()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 2));
        await ctx.Cash.OpenAsync(new CashOpenDto(0, null));

        await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 3, ("Efectivo", 600))));
        Assert.Equal(2, await ctx.StockOfAsync(dye.Id));

        var sale = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 200)));
        Assert.Equal("V-000001", sale.Folio);
    }

    [Fact]
    public async Task Insufficient_payment_is_rejected()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 2));
        await ctx.Cash.OpenAsync(new CashOpenDto(0, null));

        var ex = await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 150))));
        Assert.Contains("insuficiente", ex.Message);
    }

    [Fact]
    public async Task Cancel_returns_stock_to_the_original_lots_once()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("LATE", "2028-06-30", 5), ("SOON", "2027-01-31", 3));
        await ctx.Cash.OpenAsync(new CashOpenDto(0, null));
        var sale = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 4, ("Efectivo", 800)));

        var cancelled = await ctx.Sales.CancelAsync(sale.Id, new SaleCancelDto("Cliente se arrepintió"));

        Assert.Equal(SaleStatus.Cancelled, cancelled.Status);
        Assert.Equal(8, await ctx.StockOfAsync(dye.Id));
        var lots = await ctx.LotsOfAsync(dye.Id);
        Assert.Equal(3, lots["SOON"]);
        Assert.Equal(5, lots["LATE"]);
        await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CancelAsync(sale.Id, new SaleCancelDto("otra vez")));
    }

    [Fact]
    public async Task Cancel_is_blocked_after_the_cash_session_closes()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 2));
        var session = await ctx.Cash.OpenAsync(new CashOpenDto(0, null));
        var sale = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 200)));
        await ctx.Cash.CloseAsync(session.Id, new CashCloseDto(200, null));

        await Assert.ThrowsAsync<DomainException>(() => ctx.Sales.CancelAsync(sale.Id, new SaleCancelDto("tarde")));
    }
}

public class CashSessionServiceTests
{
    [Fact]
    public async Task Close_compares_counted_cash_with_opening_plus_cash_sales_and_movements()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 10));
        var session = await ctx.Cash.OpenAsync(new CashOpenDto(500, null));

        await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 500)));           // +200 cash
        await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Tarjeta", 200)));            // card only
        var cancelled = await ctx.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 1, ("Efectivo", 200)));
        await ctx.Sales.CancelAsync(cancelled.Id, new SaleCancelDto("error"));                    // excluded
        await ctx.Cash.AddMovementAsync(session.Id, new CashMovementSaveDto("Ingreso", 50, "Cambio"));
        await ctx.Cash.AddMovementAsync(session.Id, new CashMovementSaveDto("Retiro", 120, "Proveedor"));

        var closed = await ctx.Cash.CloseAsync(session.Id, new CashCloseDto(625, "faltante"));

        Assert.Equal(630, closed.ExpectedCash);   // 500 + 200 + 50 - 120
        Assert.Equal(-5, closed.Difference);
        Assert.Equal(200, closed.CardSales);
        Assert.Equal(2, closed.SalesCount);
        Assert.Equal(CashSessionStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task A_cashier_cannot_open_two_sessions_or_operate_someone_elses()
    {
        using var ctx = new PosTestContext();
        var session = await ctx.Cash.OpenAsync(new CashOpenDto(0, null));
        await Assert.ThrowsAsync<ConflictException>(() => ctx.Cash.OpenAsync(new CashOpenDto(0, null)));

        ctx.User.UserId = "cashier-2";
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ctx.Cash.AddMovementAsync(session.Id, new CashMovementSaveDto("Retiro", 10, "x")));
    }
}

public class InventoryTests
{
    [Fact]
    public async Task Lot_tracked_purchase_requires_a_lot_number()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await Assert.ThrowsAsync<DomainException>(() => ctx.Purchases.ReceiveAsync(
            new(null, null, null, [new(dye.Id, 3, 90, null, null)])));
    }

    [Fact]
    public async Task Same_lot_on_two_lines_merges_into_one_batch()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("a1", "2027-05-01", 2), ("A1", "2027-05-01", 3));

        var lots = await ctx.LotsOfAsync(dye.Id);
        Assert.Single(lots);
        Assert.Equal(5, lots["A1"]);
    }

    [Fact]
    public async Task Negative_adjustment_on_a_lot_cannot_exceed_its_quantity()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 2), ("B", "2028-01-31", 4));

        await Assert.ThrowsAsync<DomainException>(() => ctx.Inventory.AdjustAsync(new(dye.Id, -3, "Caducado", "A", null)));
        var movement = await ctx.Inventory.AdjustAsync(new(dye.Id, -2, "Caducado", "A", null));

        Assert.Equal(MovementTypes.AdjustmentOut, movement.Type);
        Assert.Equal(4, await ctx.StockOfAsync(dye.Id));
    }

    [Fact]
    public async Task Variant_search_matches_shade_and_reports_low_stock()
    {
        using var ctx = new PosTestContext();
        var dye = await ctx.CreateDyeAsync();
        await ctx.ReceiveAsync(dye.Id, ("A", "2027-01-31", 1));

        var found = await ctx.Products.SearchVariantsAsync("7.1");
        var low = await ctx.Products.LowStockAsync();

        Assert.Equal(dye.Id, Assert.Single(found).Id);
        Assert.Equal(dye.Id, Assert.Single(low).Id);
    }
}
