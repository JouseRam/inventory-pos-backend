using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>Reads entities into their sync records, translating local foreign keys into global Uids.</summary>
public class SyncMapper(ApplicationDbContext db)
{
    // ── Catalog (central → register, and the first upload of a register's own catalog) ──

    public async Task<List<BrandSync>> BrandsAsync(DateTime? since, CancellationToken ct) =>
        await db.Brands.AsNoTracking().Where(Changed<Brand>(since))
            .Select(b => new BrandSync(b.Uid, b.Name, b.IsActive)).ToListAsync(ct);

    public async Task<List<CategorySync>> CategoriesAsync(DateTime? since, CancellationToken ct) =>
        await db.Categories.AsNoTracking().Where(Changed<Category>(since))
            .Select(c => new CategorySync(c.Uid, c.Name, c.Description, c.Parent != null ? c.Parent.Uid : null, c.IsActive)).ToListAsync(ct);

    public async Task<List<SupplierSync>> SuppliersAsync(DateTime? since, CancellationToken ct) =>
        await db.Suppliers.AsNoTracking().Where(Changed<Supplier>(since))
            .Select(s => new SupplierSync(s.Uid, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive)).ToListAsync(ct);

    public async Task<List<ProductSync>> ProductsAsync(DateTime? since, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(Changed<Product>(since))
            .Select(p => new ProductSync(p.Uid, p.Name, p.Description,
                p.Brand != null ? p.Brand.Uid : null, p.Category != null ? p.Category.Uid : null, p.Supplier != null ? p.Supplier.Uid : null,
                p.TracksBatches, p.IsActive)).ToListAsync(ct);

    public async Task<List<VariantSync>> VariantsAsync(DateTime? since, CancellationToken ct) =>
        await db.ProductVariants.AsNoTracking().Where(Changed<ProductVariant>(since))
            .Select(v => new VariantSync(v.Uid, v.Product.Uid, v.SKU, v.Barcode, v.ShadeCode, v.ShadeName, v.Size, v.Unit,
                v.Price, v.Cost, v.MinStock, v.IsActive)).ToListAsync(ct);

    public async Task<List<CustomerSync>> CustomersAsync(DateTime? since, IReadOnlyCollection<Guid>? uids, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking().Where(Changed<Customer>(since));
        if (uids != null) query = query.Where(c => uids.Contains(c.Uid));
        return await query.Select(c => new CustomerSync(c.Uid, c.Name, c.Phone, c.Email, c.Address, c.Notes, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<List<SettingSync>> SettingsAsync(DateTime? since, CancellationToken ct) =>
        await db.Settings.AsNoTracking().Where(Changed<Setting>(since)).Where(s => s.Group != Application.Features.Settings.SettingService.LocalGroup)
            .Select(s => new SettingSync(s.Key, s.Value, s.Label, s.Type, s.Group)).ToListAsync(ct);

    public async Task<List<UserSync>> UsersAsync(DateTime? since, IReadOnlyCollection<string>? ids, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (since.HasValue) query = query.Where(u => u.UpdatedAt >= since.Value);
        if (ids != null) query = query.Where(u => ids.Contains(u.Id));
        var users = await query.ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();
        var permissions = (await db.UserModulePermissions.AsNoTracking().Where(p => userIds.Contains(p.UserId)).ToListAsync(ct))
            .ToLookup(p => p.UserId);
        return users.Select(u => new UserSync(u.Id, u.UserName!, u.Email, u.FullName, u.IsSuperAdmin, u.IsActive, u.PasswordHash, u.SecurityStamp,
            permissions[u.Id].Select(p => new PermissionSync(p.Module, p.CanView, p.CanEdit)).ToList())).ToList();
    }

    // ── Operations (register → central) ──

    public async Task<List<BatchSync>> BatchesAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct) =>
        await db.ProductBatches.AsNoTracking().Where(b => uids.Contains(b.Uid))
            .Select(b => new BatchSync(b.Uid, b.Variant.Uid, b.LotNumber, b.ExpirationDate, b.Quantity, b.CreatedAt)).ToListAsync(ct);

    public async Task<List<MovementSync>> MovementsAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct) =>
        await db.InventoryMovements.AsNoTracking().Where(m => uids.Contains(m.Uid))
            .Select(m => new MovementSync(m.Uid, m.Variant.Uid, m.Batch != null ? m.Batch.Uid : null, m.Type, m.Quantity, m.StockAfter, m.UnitCost,
                m.Reference, m.Notes, m.UserId, m.CreatedAt)).ToListAsync(ct);

    public async Task<List<PurchaseSync>> PurchasesAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct)
    {
        var purchases = await db.Purchases.AsNoTracking().Where(p => uids.Contains(p.Uid))
            .Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Variant).Include(p => p.Items).ThenInclude(i => i.Batch)
            .AsSplitQuery().ToListAsync(ct);
        return purchases.Select(p => new PurchaseSync(p.Uid, p.Folio, p.Supplier?.Uid, p.InvoiceNumber, p.Total, p.Notes, p.UserId, p.CreatedAt,
            p.Items.Select(i => new PurchaseItemSync(i.Uid, i.Variant.Uid, i.Quantity, i.UnitCost, i.Subtotal, i.LotNumber, i.ExpirationDate, i.Batch?.Uid))
                .ToList())).ToList();
    }

    public async Task<List<CashSessionSync>> CashSessionsAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct)
    {
        var sessions = await db.CashSessions.AsNoTracking().Where(s => uids.Contains(s.Uid)).Include(s => s.Movements).ToListAsync(ct);
        return sessions.Select(s => new CashSessionSync(s.Uid, s.Folio, s.UserId, s.Status, s.OpeningAmount, s.OpenedAt, s.ClosedAt,
            s.ExpectedCash, s.CountedCash, s.Difference, s.Notes,
            s.Movements.Select(m => new CashMovementSync(m.Uid, m.Type, m.Amount, m.Reason, m.UserId, m.CreatedAt)).ToList())).ToList();
    }

    public async Task<List<SaleSync>> SalesAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct)
    {
        var sales = await db.Sales.AsNoTracking().Where(s => uids.Contains(s.Uid))
            .Include(s => s.Customer).Include(s => s.CashSession).Include(s => s.Items).ThenInclude(i => i.Variant).Include(s => s.Payments)
            .AsSplitQuery().ToListAsync(ct);
        return sales.Select(s => new SaleSync(s.Uid, s.Folio, s.Customer?.Uid, s.CashSession.Uid, s.Subtotal, s.Discount, s.Tax, s.Total,
            s.AmountPaid, s.Change, s.Status, s.Notes, s.CancelReason, s.CancelledAt, s.UserId, s.CreatedAt,
            s.Items.Select(i => new SaleItemSync(i.Uid, i.Variant.Uid, i.Description, i.Quantity, i.UnitPrice, i.UnitCost, i.Discount, i.Subtotal)).ToList(),
            s.Payments.Select(p => new SalePaymentSync(p.Uid, p.Method, p.Amount, p.Reference)).ToList())).ToList();
    }

    public async Task<List<StockSync>> StocksAsync(IReadOnlyCollection<Guid> variantUids, CancellationToken ct) =>
        await db.ProductVariants.AsNoTracking().Where(v => variantUids.Contains(v.Uid))
            .Select(v => new StockSync(v.Uid, v.Stock)).ToListAsync(ct);

    /// <summary>Rows created or changed at or after the cursor (all rows when there is no cursor yet).</summary>
    private static System.Linq.Expressions.Expression<Func<T, bool>> Changed<T>(DateTime? since) where T : Domain.Common.BaseEntity =>
        since.HasValue ? e => (e.UpdatedAt ?? e.CreatedAt) >= since.Value : _ => true;
}
