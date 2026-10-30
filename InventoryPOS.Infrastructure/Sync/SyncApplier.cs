using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>
/// Writes incoming sync records with upserts keyed by Uid, so receiving the same batch twice changes nothing.
/// The caller owns the transaction. <paramref name="overwrite"/> decides whether existing records are updated
/// (the register accepting the server's catalog) or kept (the server receiving a register's first catalog upload).
/// </summary>
public class SyncApplier(ApplicationDbContext db, ILookupNormalizer normalizer)
{
    private readonly Dictionary<(Type, Guid), int> _ids = [];

    // ── Catalog ──

    public async Task ApplyCatalogAsync(IReadOnlyList<BrandSync> brands, IReadOnlyList<CategorySync> categories, IReadOnlyList<SupplierSync> suppliers,
        IReadOnlyList<ProductSync> products, IReadOnlyList<VariantSync> variants, bool overwrite, CancellationToken ct)
    {
        foreach (var b in brands)
            await UpsertAsync<Brand>(b.Uid, overwrite, e => { e.Name = b.Name; e.IsActive = b.IsActive; }, ct);
        await db.SaveChangesAsync(ct);

        // Two passes so a subcategory can reference a parent that arrives in the same batch.
        foreach (var c in categories)
            await UpsertAsync<Category>(c.Uid, overwrite, e => { e.Name = c.Name; e.Description = c.Description; e.IsActive = c.IsActive; }, ct);
        await db.SaveChangesAsync(ct);
        foreach (var c in categories.Where(c => c.ParentUid.HasValue))
        {
            var entity = await db.Categories.FirstAsync(x => x.Uid == c.Uid, ct);
            if (overwrite || entity.ParentId == null) entity.ParentId = await IdOfAsync<Category>(c.ParentUid, ct);
        }
        await db.SaveChangesAsync(ct);

        foreach (var s in suppliers)
            await UpsertAsync<Supplier>(s.Uid, overwrite, e =>
            {
                e.Name = s.Name; e.ContactName = s.ContactName; e.Phone = s.Phone; e.Email = s.Email; e.Address = s.Address; e.IsActive = s.IsActive;
            }, ct);
        await db.SaveChangesAsync(ct);

        foreach (var p in products)
        {
            var brandId = await IdOfAsync<Brand>(p.BrandUid, ct);
            var categoryId = await IdOfAsync<Category>(p.CategoryUid, ct);
            var supplierId = await IdOfAsync<Supplier>(p.SupplierUid, ct);
            await UpsertAsync<Product>(p.Uid, overwrite, e =>
            {
                e.Name = p.Name; e.Description = p.Description; e.BrandId = brandId; e.CategoryId = categoryId; e.SupplierId = supplierId;
                e.TracksBatches = p.TracksBatches; e.IsActive = p.IsActive;
            }, ct);
        }
        await db.SaveChangesAsync(ct);

        foreach (var v in variants)
        {
            var productId = await IdOfAsync<Product>(v.ProductUid, ct) ?? throw Missing("producto", v.ProductUid);
            // Stock is never taken from the catalog: the register owns it.
            await UpsertAsync<ProductVariant>(v.Uid, overwrite, e =>
            {
                e.ProductId = productId; e.SKU = v.SKU; e.Barcode = v.Barcode; e.ShadeCode = v.ShadeCode; e.ShadeName = v.ShadeName;
                e.Size = v.Size; e.Unit = v.Unit; e.Price = v.Price; e.Cost = v.Cost; e.MinStock = v.MinStock; e.IsActive = v.IsActive;
            }, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Customers are edited on both sides: the most recent change wins.</summary>
    public async Task ApplyCustomersAsync(IReadOnlyList<CustomerSync> customers, CancellationToken ct)
    {
        foreach (var c in customers)
        {
            var entity = await db.Customers.FirstOrDefaultAsync(x => x.Uid == c.Uid, ct);
            var incoming = c.UpdatedAt ?? c.CreatedAt;
            if (entity != null && (entity.UpdatedAt ?? entity.CreatedAt) > incoming) continue;
            if (entity == null) db.Customers.Add(entity = new Customer { Uid = c.Uid, CreatedAt = c.CreatedAt });
            entity.Name = c.Name; entity.Phone = c.Phone; entity.Email = c.Email; entity.Address = c.Address; entity.Notes = c.Notes;
            entity.IsActive = c.IsActive;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplySettingsAsync(IReadOnlyList<SettingSync> settings, CancellationToken ct)
    {
        foreach (var s in settings)
        {
            var entity = await db.Settings.FirstOrDefaultAsync(x => x.Key == s.Key, ct);
            if (entity == null) db.Settings.Add(entity = new Setting { Key = s.Key });
            entity.Value = s.Value; entity.Label = s.Label; entity.Type = s.Type; entity.Group = s.Group;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Accounts keyed by their id (identical on both sides), with password hash and permissions.</summary>
    public async Task ApplyUsersAsync(IReadOnlyList<UserSync> users, bool overwrite, CancellationToken ct)
    {
        foreach (var u in users)
        {
            var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == u.Id, ct);
            if (entity != null && !overwrite) continue;
            var normalized = normalizer.NormalizeName(u.UserName);
            if (entity == null)
            {
                // Another account already uses this name: the server's account takes precedence.
                if (await db.Users.AnyAsync(x => x.NormalizedUserName == normalized, ct)) continue;
                db.Users.Add(entity = new AppUser { Id = u.Id });
            }
            entity.UserName = u.UserName; entity.NormalizedUserName = normalized;
            entity.Email = u.Email; entity.NormalizedEmail = u.Email == null ? null : normalizer.NormalizeEmail(u.Email);
            entity.FullName = u.FullName; entity.IsSuperAdmin = u.IsSuperAdmin; entity.IsActive = u.IsActive;
            entity.PasswordHash = u.PasswordHash; entity.SecurityStamp = u.SecurityStamp ?? Guid.NewGuid().ToString("N");
            entity.ConcurrencyStamp = Guid.NewGuid().ToString(); entity.UpdatedAt = DateTime.UtcNow;

            db.UserModulePermissions.RemoveRange(await db.UserModulePermissions.Where(p => p.UserId == u.Id).ToListAsync(ct));
            foreach (var p in u.Permissions)
                db.UserModulePermissions.Add(new UserModulePermission { UserId = u.Id, Module = p.Module, CanView = p.CanView, CanEdit = p.CanEdit });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── Operations (central receiving a register) ──

    public async Task<int> ApplyOperationsAsync(PushRequest push, CancellationToken ct)
    {
        var applied = 0;
        await ApplyCustomersAsync(push.Customers, ct);
        applied += push.Customers.Count;

        foreach (var b in push.Batches)
        {
            var variantId = await IdOfAsync<ProductVariant>(b.VariantUid, ct) ?? throw Missing("presentación", b.VariantUid);
            await UpsertAsync<ProductBatch>(b.Uid, overwrite: true, e =>
            {
                e.VariantId = variantId; e.LotNumber = b.LotNumber; e.ExpirationDate = b.ExpirationDate; e.Quantity = b.Quantity; e.CreatedAt = b.CreatedAt;
            }, ct);
            applied++;
        }
        await db.SaveChangesAsync(ct);

        foreach (var p in push.Purchases)
        {
            if (await db.Purchases.AnyAsync(x => x.Uid == p.Uid, ct)) continue;
            await EnsureUserAsync(p.UserId, ct);
            var purchase = new Purchase
            {
                Uid = p.Uid, Folio = p.Folio, SupplierId = await IdOfAsync<Supplier>(p.SupplierUid, ct), InvoiceNumber = p.InvoiceNumber,
                Total = p.Total, Notes = p.Notes, UserId = p.UserId, CreatedAt = p.CreatedAt,
            };
            foreach (var i in p.Items)
                purchase.Items.Add(new PurchaseItem
                {
                    Uid = i.Uid, VariantId = await IdOfAsync<ProductVariant>(i.VariantUid, ct) ?? throw Missing("presentación", i.VariantUid),
                    Quantity = i.Quantity, UnitCost = i.UnitCost, Subtotal = i.Subtotal, LotNumber = i.LotNumber, ExpirationDate = i.ExpirationDate,
                    BatchId = await IdOfAsync<ProductBatch>(i.BatchUid, ct),
                });
            db.Purchases.Add(purchase);
            applied++;
        }
        await db.SaveChangesAsync(ct);

        foreach (var s in push.CashSessions)
        {
            await EnsureUserAsync(s.UserId, ct);
            var session = await db.CashSessions.Include(x => x.Movements).FirstOrDefaultAsync(x => x.Uid == s.Uid, ct);
            if (session == null) db.CashSessions.Add(session = new CashSession { Uid = s.Uid });
            session.Folio = s.Folio; session.UserId = s.UserId; session.Status = s.Status; session.OpeningAmount = s.OpeningAmount;
            session.OpenedAt = s.OpenedAt; session.ClosedAt = s.ClosedAt; session.ExpectedCash = s.ExpectedCash; session.CountedCash = s.CountedCash;
            session.Difference = s.Difference; session.Notes = s.Notes;
            foreach (var m in s.Movements.Where(m => session.Movements.All(x => x.Uid != m.Uid)))
            {
                await EnsureUserAsync(m.UserId, ct);
                session.Movements.Add(new CashMovement { Uid = m.Uid, Type = m.Type, Amount = m.Amount, Reason = m.Reason, UserId = m.UserId, CreatedAt = m.CreatedAt });
            }
            applied++;
        }
        await db.SaveChangesAsync(ct);

        foreach (var s in push.Sales)
        {
            var sale = await db.Sales.FirstOrDefaultAsync(x => x.Uid == s.Uid, ct);
            if (sale == null)
            {
                await EnsureUserAsync(s.UserId, ct);
                sale = new Sale
                {
                    Uid = s.Uid, Folio = s.Folio, CustomerId = await IdOfAsync<Customer>(s.CustomerUid, ct),
                    CashSessionId = await IdOfAsync<CashSession>(s.CashSessionUid, ct) ?? throw Missing("caja", s.CashSessionUid),
                    Subtotal = s.Subtotal, Discount = s.Discount, Tax = s.Tax, Total = s.Total, AmountPaid = s.AmountPaid, Change = s.Change,
                    UserId = s.UserId, CreatedAt = s.CreatedAt,
                };
                foreach (var i in s.Items)
                    sale.Items.Add(new SaleItem
                    {
                        Uid = i.Uid, VariantId = await IdOfAsync<ProductVariant>(i.VariantUid, ct) ?? throw Missing("presentación", i.VariantUid),
                        Description = i.Description, Quantity = i.Quantity, UnitPrice = i.UnitPrice, UnitCost = i.UnitCost, Discount = i.Discount,
                        Subtotal = i.Subtotal,
                    });
                foreach (var p in s.Payments)
                    sale.Payments.Add(new SalePayment { Uid = p.Uid, Method = p.Method, Amount = p.Amount, Reference = p.Reference });
                db.Sales.Add(sale);
            }
            // Only the outcome of a sale can change after it is registered (a cancellation).
            sale.Status = s.Status; sale.Notes = s.Notes; sale.CancelReason = s.CancelReason; sale.CancelledAt = s.CancelledAt;
            applied++;
        }
        await db.SaveChangesAsync(ct);

        foreach (var m in push.Movements)
        {
            if (await db.InventoryMovements.AnyAsync(x => x.Uid == m.Uid, ct)) continue;
            db.InventoryMovements.Add(new InventoryMovement
            {
                Uid = m.Uid, VariantId = await IdOfAsync<ProductVariant>(m.VariantUid, ct) ?? throw Missing("presentación", m.VariantUid),
                BatchId = await IdOfAsync<ProductBatch>(m.BatchUid, ct), Type = m.Type, Quantity = m.Quantity, StockAfter = m.StockAfter,
                UnitCost = m.UnitCost, Reference = m.Reference, Notes = m.Notes, UserId = m.UserId, CreatedAt = m.CreatedAt,
            });
            applied++;
        }
        await db.SaveChangesAsync(ct);

        foreach (var s in push.Stocks)
        {
            var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Uid == s.VariantUid, ct);
            if (variant == null) continue;
            variant.Stock = s.Stock;
            applied++;
        }
        await db.SaveChangesAsync(ct);
        return applied;
    }

    // ── Helpers ──

    private async Task UpsertAsync<T>(Guid uid, bool overwrite, Action<T> apply, CancellationToken ct) where T : BaseEntity, new()
    {
        var entity = await db.Set<T>().FirstOrDefaultAsync(e => e.Uid == uid, ct);
        if (entity != null && !overwrite) return;
        if (entity == null) db.Set<T>().Add(entity = new T { Uid = uid });
        apply(entity);
    }

    private async Task<int?> IdOfAsync<T>(Guid? uid, CancellationToken ct) where T : BaseEntity
    {
        if (uid is not { } key) return null;
        if (_ids.TryGetValue((typeof(T), key), out var cached)) return cached;
        var id = await db.Set<T>().Where(e => e.Uid == key).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct);
        if (id.HasValue) _ids[(typeof(T), key)] = id.Value;
        return id;
    }

    /// <summary>Records keep their author's id; an account the server does not know gets an inactive placeholder.</summary>
    private async Task EnsureUserAsync(string? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId) || await db.Users.AnyAsync(u => u.Id == userId, ct)
            || db.Users.Local.Any(u => u.Id == userId)) return;
        var name = $"caja-{userId[..Math.Min(8, userId.Length)]}";
        db.Users.Add(new AppUser
        {
            Id = userId, UserName = name, NormalizedUserName = normalizer.NormalizeName(name), FullName = "Usuario de caja",
            IsActive = false, SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        await db.SaveChangesAsync(ct);
    }

    private static DomainException Missing(string what, Guid uid) =>
        new($"El servidor no tiene la {what} {uid}; sincroniza el catálogo antes de enviar operaciones.");
}
