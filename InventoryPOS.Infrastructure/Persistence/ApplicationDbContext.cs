using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InventoryPOS.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<AppUser>(options)
{
    // ── Catalogs ──
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();

    // ── Products and inventory ──
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    // ── Sales and cash register ──
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();

    // ── System ──
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<UserModulePermission> UserModulePermissions => Set<UserModulePermission>();
    public DbSet<FolioSequence> FolioSequences => Set<FolioSequence>();

    // ── Sync ──
    public DbSet<SyncOutbox> SyncOutbox => Set<SyncOutbox>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<SyncDevice> SyncDevices => Set<SyncDevice>();

    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        IndexSyncIdentities(builder);
        MarkTimestampsAsUtc(builder);
    }

    /// <summary>Every entity is looked up by its global Uid when records travel between register and server.</summary>
    private static void IndexSyncIdentities(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes().Where(e => typeof(Domain.Common.BaseEntity).IsAssignableFrom(e.ClrType)))
            builder.Entity(entity.ClrType).HasIndex(nameof(Domain.Common.BaseEntity.Uid)).IsUnique();
    }

    /// <summary>
    /// Timestamps are stored in UTC; reading them back as Kind=Utc makes the API serialize them with "Z"
    /// so the browser converts to local time. Calendar dates (expiration) are left untouched.
    /// </summary>
    private static void MarkTimestampsAsUtc(ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.Name == nameof(ProductBatch.ExpirationDate)) continue;
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(UtcConverter);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(NullableUtcConverter);
        }
    }
}
