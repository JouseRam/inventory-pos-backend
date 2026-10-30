using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using InventoryPOS.Infrastructure.Persistence;
using InventoryPOS.Infrastructure.Services;
using InventoryPOS.Infrastructure.Sync;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Application.Tests;

public class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = "cashier-1";
    public string? UserName => "cajero";
    public bool IsSuperAdmin { get; set; }
}

public class FakeEnvironment(PosRole role) : IPosEnvironment
{
    public PosRole Role { get; } = role;
    public bool Paired { get; set; }
    public Task<bool> IsPairedAsync(CancellationToken ct = default) => Task.FromResult(Paired);
}

/// <summary>Real services and repositories over an in-memory SQLite database, one per test.</summary>
public sealed class PosTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }
    public FakeCurrentUser User { get; } = new();
    public UnitOfWork Uow { get; }
    public StockService Stock { get; }
    public ProductService Products { get; }
    public PurchaseService Purchases { get; }
    public InventoryService Inventory { get; }
    public SaleService Sales { get; }
    public CashSessionService Cash { get; }
    public FakeEnvironment Env { get; }

    public PosTestContext(PosRole role = PosRole.Register)
    {
        Env = new FakeEnvironment(role);
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection)
            .AddInterceptors(new SyncSaveChangesInterceptor(role)).Options);
        Db.Database.EnsureCreated();

        Db.Users.Add(new AppUser { Id = "cashier-1", UserName = "cajero", FullName = "Cajero" });
        Db.Users.Add(new AppUser { Id = "cashier-2", UserName = "cajero2", FullName = "Cajero 2" });
        Db.FolioSequences.AddRange(
            new FolioSequence { Name = FolioSeries.Sale, Prefix = "V-" },
            new FolioSequence { Name = FolioSeries.Purchase, Prefix = "REC-" },
            new FolioSequence { Name = FolioSeries.CashSession, Prefix = "CJ-" });
        Db.Settings.Add(new Setting { Key = SettingKeys.TaxRate, Value = "16", Label = "IVA", Type = "number" });
        Db.SaveChanges();

        var clock = new DateTimeProvider(DateTimeProvider.ResolveTimeZone(null));
        var folios = new FolioGenerator(Db);
        Uow = new UnitOfWork(Db);
        Stock = new StockService(Uow, User);
        var guard = new PosGuard(Env);
        var settings = new SettingService(Uow, guard);
        Products = new ProductService(Uow, Stock, guard);
        Purchases = new PurchaseService(Uow, Stock, folios, User, clock, guard);
        Inventory = new InventoryService(Uow, Stock, clock, guard);
        Sales = new SaleService(Uow, Stock, folios, settings, User, clock, guard);
        Cash = new CashSessionService(Uow, folios, User, clock, guard);
    }

    /// <summary>A lot-tracked dye with one shade, priced at 200.</summary>
    public async Task<VariantDto> CreateDyeAsync(string sku = "TINTE-71")
    {
        var product = await Products.CreateAsync(new ProductCreateDto("Tinte Prueba", null, null, null, null, TracksBatches: true,
            [new VariantSaveDto(sku, null, "7.1", "Rubio ceniza", 60, UnitsOfMeasure.Milliliter, Price: 200, Cost: 100, MinStock: 2)]));
        return product.Variants.Single();
    }

    public Task<PurchaseDto> ReceiveAsync(int variantId, params (string Lot, string Expires, decimal Qty)[] lots) =>
        Purchases.ReceiveAsync(new PurchaseCreateDto(null, null, null,
            lots.Select(l => new PurchaseItemSaveDto(variantId, l.Qty, 100, l.Lot, DateTime.Parse(l.Expires))).ToList()));

    public async Task<decimal> StockOfAsync(int variantId)
    {
        Db.ChangeTracker.Clear();
        return (await Db.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == variantId)).Stock;
    }

    public async Task<Dictionary<string, decimal>> LotsOfAsync(int variantId)
    {
        Db.ChangeTracker.Clear();
        return await Db.ProductBatches.AsNoTracking().Where(b => b.VariantId == variantId)
            .ToDictionaryAsync(b => b.LotNumber, b => b.Quantity);
    }

    public static SaleCreateDto Sale(int variantId, decimal qty, params (string Method, decimal Amount)[] payments) =>
        new(null, 0, null, [new SaleItemSaveDto(variantId, qty, null)], payments.Select(p => new SalePaymentSaveDto(p.Method, p.Amount, null)).ToList());

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
