using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Infrastructure.Sync;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

/// <summary>Register → central round trips through the real mapper and applier, without HTTP.</summary>
public class SyncTests
{
    private static async Task<PushRequest> BuildPushAsync(PosTestContext register, bool uploadCatalog)
    {
        var mapper = new SyncMapper(register.Db);
        var rows = await register.Db.SyncOutbox.AsNoTracking().ToListAsync();
        HashSet<Guid> Of(string type) => rows.Where(r => r.EntityType == type).Select(r => r.EntityUid).ToHashSet();
        var sales = await mapper.SalesAsync(Of(SyncTypes.Sale), default);
        var movements = await mapper.MovementsAsync(Of(SyncTypes.Movement), default);
        var batchUids = Of(SyncTypes.Batch);
        batchUids.UnionWith(movements.Where(m => m.BatchUid.HasValue).Select(m => m.BatchUid!.Value));
        var sessionUids = Of(SyncTypes.CashSession);
        sessionUids.UnionWith(sales.Select(s => s.CashSessionUid));
        return new PushRequest(
            await mapper.CustomersAsync(null, Of(SyncTypes.Customer), default), await mapper.BatchesAsync(batchUids, default),
            await mapper.PurchasesAsync(Of(SyncTypes.Purchase), default), await mapper.CashSessionsAsync(sessionUids, default),
            sales, movements, await mapper.StocksAsync(Of(SyncTypes.Stock), default),
            uploadCatalog ? await mapper.BrandsAsync(null, default) : null, uploadCatalog ? await mapper.CategoriesAsync(null, default) : null,
            uploadCatalog ? await mapper.SuppliersAsync(null, default) : null, uploadCatalog ? await mapper.ProductsAsync(null, default) : null,
            uploadCatalog ? await mapper.VariantsAsync(null, default) : null, uploadCatalog ? await mapper.UsersAsync(null, null, default) : null);
    }

    private static async Task ApplyOnCentralAsync(PosTestContext central, PushRequest push)
    {
        var applier = new SyncApplier(central.Db, new UpperInvariantLookupNormalizer());
        await using var tx = await central.Db.Database.BeginTransactionAsync();
        if (push.Users != null) await applier.ApplyUsersAsync(push.Users, overwrite: false, default);
        if (push.Products != null)
            await applier.ApplyCatalogAsync(push.Brands!, push.Categories!, push.Suppliers!, push.Products, push.Variants!, overwrite: false, default);
        await applier.ApplyOperationsAsync(push, default);
        await tx.CommitAsync();
        central.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task A_sale_queues_its_aggregate_movements_and_stock_in_the_same_save()
    {
        using var register = new PosTestContext();
        var dye = await register.CreateDyeAsync();
        await register.ReceiveAsync(dye.Id, ("A", "2027-01-31", 5));
        await register.Cash.OpenAsync(new CashOpenDto(0, null));
        await register.Db.SyncOutbox.ExecuteDeleteAsync();

        await register.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 2, ("Efectivo", 400)));

        var types = await register.Db.SyncOutbox.Select(o => o.EntityType).Distinct().ToListAsync();
        Assert.Contains(SyncTypes.Sale, types);
        Assert.Contains(SyncTypes.Movement, types);
        Assert.Contains(SyncTypes.Stock, types);
        Assert.Contains(SyncTypes.Batch, types);
    }

    [Fact]
    public async Task Pushing_the_same_batch_twice_does_not_duplicate_anything()
    {
        using var register = new PosTestContext();
        using var central = new PosTestContext(PosRole.Central);
        var dye = await register.CreateDyeAsync();
        await register.ReceiveAsync(dye.Id, ("SOON", "2027-01-31", 3), ("LATE", "2028-06-30", 5));
        await register.Cash.OpenAsync(new CashOpenDto(100, null));
        var sale = await register.Sales.CreateAsync(PosTestContext.Sale(dye.Id, 4, ("Tarjeta", 150), ("Efectivo", 700)));
        await register.Sales.CancelAsync(sale.Id, new SaleCancelDto("prueba"));

        var push = await BuildPushAsync(register, uploadCatalog: true);
        await ApplyOnCentralAsync(central, push);
        await ApplyOnCentralAsync(central, push);

        var c = central.Db;
        Assert.Equal(1, await c.Sales.CountAsync());
        Assert.Equal("Cancelada", (await c.Sales.SingleAsync()).Status);
        Assert.Equal(sale.Folio, (await c.Sales.SingleAsync()).Folio);
        Assert.Equal(2, await c.SalePayments.CountAsync());
        Assert.Equal(await register.Db.InventoryMovements.CountAsync(), await c.InventoryMovements.CountAsync());
        Assert.Equal(2, await c.ProductBatches.CountAsync());
        Assert.Equal(8, (await c.ProductVariants.SingleAsync()).Stock);
        Assert.Equal(1, await c.CashSessions.CountAsync());
        Assert.Equal(1, await c.Purchases.CountAsync());
    }

    [Fact]
    public async Task Central_keeps_its_catalog_when_a_register_uploads_one()
    {
        using var register = new PosTestContext();
        using var central = new PosTestContext(PosRole.Central);
        var dye = await register.CreateDyeAsync();
        await ApplyOnCentralAsync(central, await BuildPushAsync(register, uploadCatalog: true));

        var variant = await register.Db.ProductVariants.SingleAsync();
        variant.Price = 999;
        await register.Db.SaveChangesAsync();
        await ApplyOnCentralAsync(central, await BuildPushAsync(register, uploadCatalog: true));

        Assert.Equal(dye.Price, (await central.Db.ProductVariants.SingleAsync()).Price);
    }

    [Fact]
    public async Task Pulled_catalog_updates_prices_but_never_touches_the_register_stock()
    {
        using var register = new PosTestContext();
        var dye = await register.CreateDyeAsync();
        await register.ReceiveAsync(dye.Id, ("A", "2027-01-31", 6));
        var v = await register.Db.ProductVariants.AsNoTracking().Include(x => x.Product).SingleAsync();

        var pulled = new VariantSync(v.Uid, v.Product.Uid, v.SKU, v.Barcode, v.ShadeCode, v.ShadeName, v.Size, v.Unit, 249m, 120m, 4, true);
        register.Db.ChangeTracker.Clear();
        SyncSaveChangesInterceptor.SuppressOutbox.Value = true;
        try
        {
            await register.Db.SyncOutbox.ExecuteDeleteAsync();
            await new SyncApplier(register.Db, new UpperInvariantLookupNormalizer()).ApplyCatalogAsync([], [], [], [], [pulled], overwrite: true, default);
        }
        finally { SyncSaveChangesInterceptor.SuppressOutbox.Value = false; }

        var after = await register.Db.ProductVariants.AsNoTracking().SingleAsync();
        Assert.Equal(249m, after.Price);
        Assert.Equal(6, after.Stock);
        Assert.Equal(0, await register.Db.SyncOutbox.CountAsync());
    }

    [Fact]
    public async Task A_paired_register_cannot_edit_the_catalog_and_the_web_cannot_sell()
    {
        using var register = new PosTestContext();
        var dye = await register.CreateDyeAsync("SKU-ANTES");
        register.Env.Paired = true;
        var ex = await Assert.ThrowsAsync<DomainException>(() => register.CreateDyeAsync("SKU-DESPUES"));
        Assert.Contains("web", ex.Message);

        using var central = new PosTestContext(PosRole.Central);
        await Assert.ThrowsAsync<DomainException>(() => central.Cash.OpenAsync(new CashOpenDto(0, null)));
    }
}
