using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Persistence.Repositories;

public class ProductRepository(ApplicationDbContext db) : Repository<Product>(db), IProductRepository
{
    private IQueryable<Product> WithDetails => Set
        .Include(p => p.Brand).Include(p => p.Category).Include(p => p.Supplier)
        .Include(p => p.Variants).AsSplitQuery();

    public Task<Product?> GetWithVariantsAsync(int id, CancellationToken ct = default) =>
        WithDetails.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> SearchAsync(string? q, int? categoryId, int? brandId, bool includeInactive, CancellationToken ct = default)
    {
        var query = WithDetails.AsNoTracking();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId || (p.Category != null && p.Category.ParentId == categoryId));
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => p.Name.Contains(term)
                || (p.Brand != null && p.Brand.Name.Contains(term))
                || p.Variants.Any(v => v.SKU.Contains(term) || (v.Barcode != null && v.Barcode == term)
                    || (v.ShadeCode != null && v.ShadeCode.StartsWith(term)) || (v.ShadeName != null && v.ShadeName.Contains(term))));
        }
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }
}

public class ProductVariantRepository(ApplicationDbContext db) : Repository<ProductVariant>(db), IProductVariantRepository
{
    private IQueryable<ProductVariant> WithProduct => Set.Include(v => v.Product).ThenInclude(p => p.Brand);

    public Task<ProductVariant?> GetWithProductAsync(int id, CancellationToken ct = default) =>
        WithProduct.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<ProductVariant>> GetByIdsWithProductAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var list = ids.ToList();
        return await WithProduct.Where(v => list.Contains(v.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductVariant>> SearchAsync(string q, int take, CancellationToken ct = default) =>
        await WithProduct.AsNoTracking()
            .Where(v => v.IsActive && v.Product.IsActive &&
                (v.Product.Name.Contains(q) || v.SKU.Contains(q) || v.Barcode == q
                 || (v.ShadeCode != null && v.ShadeCode.StartsWith(q)) || (v.ShadeName != null && v.ShadeName.Contains(q))
                 || (v.Product.Brand != null && v.Product.Brand.Name.Contains(q))))
            .OrderBy(v => v.Product.Name).ThenBy(v => v.ShadeCode).ThenBy(v => v.Size)
            .Take(take).ToListAsync(ct);

    public Task<ProductVariant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        WithProduct.AsNoTracking()
            .Where(v => v.IsActive && v.Product.IsActive && (v.Barcode == code || v.SKU == code.ToUpper()))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProductVariant>> GetLowStockAsync(int? take = null, CancellationToken ct = default)
    {
        var query = WithProduct.AsNoTracking()
            .Where(v => v.IsActive && v.Product.IsActive && v.Stock <= v.MinStock)
            .OrderBy(v => v.Stock).ThenBy(v => v.Product.Name);
        return take.HasValue ? await query.Take(take.Value).ToListAsync(ct) : await query.ToListAsync(ct);
    }
}

public class ProductBatchRepository(ApplicationDbContext db) : Repository<ProductBatch>(db), IProductBatchRepository
{
    public async Task<IReadOnlyList<ProductBatch>> GetAvailableFefoAsync(int variantId, CancellationToken ct = default) =>
        await Set.Include(b => b.Variant).ThenInclude(v => v.Product)
            .Where(b => b.VariantId == variantId && b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate == null).ThenBy(b => b.ExpirationDate).ThenBy(b => b.Id)
            .ToListAsync(ct);

    /// <summary>Checks batches added in this unit of work first so two lines with the same lot share one batch.</summary>
    public async Task<ProductBatch?> FindByLotAsync(int variantId, string lotNumber, CancellationToken ct = default) =>
        Set.Local.FirstOrDefault(b => (b.VariantId == variantId || b.Variant?.Id == variantId) && b.LotNumber == lotNumber && variantId != 0)
        ?? await Set.FirstOrDefaultAsync(b => b.VariantId == variantId && b.LotNumber == lotNumber, ct);

    public async Task<IReadOnlyList<ProductBatch>> GetExpiringAsync(DateTime untilDate, CancellationToken ct = default) =>
        await Set.AsNoTracking().Include(b => b.Variant).ThenInclude(v => v.Product)
            .Where(b => b.Quantity > 0 && b.ExpirationDate != null && b.ExpirationDate <= untilDate && b.Variant.IsActive)
            .OrderBy(b => b.ExpirationDate)
            .ToListAsync(ct);
}

public class InventoryMovementRepository(ApplicationDbContext db) : Repository<InventoryMovement>(db), IInventoryMovementRepository
{
    public Task<PagedResult<InventoryMovement>> GetPagedAsync(int? variantId, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(m => m.Variant).ThenInclude(v => v.Product).Include(m => m.Batch).AsQueryable();
        if (variantId.HasValue) query = query.Where(m => m.VariantId == variantId);
        if (fromUtc.HasValue) query = query.Where(m => m.CreatedAt >= fromUtc);
        if (toUtc.HasValue) query = query.Where(m => m.CreatedAt < toUtc);
        return PageAsync(query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id), page, pageSize, ct);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetByReferenceAsync(string reference, CancellationToken ct = default) =>
        await Set.Include(m => m.Batch).Where(m => m.Reference == reference).OrderBy(m => m.Id).ToListAsync(ct);
}

public class PurchaseRepository(ApplicationDbContext db) : Repository<Purchase>(db), IPurchaseRepository
{
    public Task<Purchase?> GetWithItemsAsync(int id, CancellationToken ct = default) =>
        Set.Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PagedResult<Purchase>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, int? supplierId, int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(p => p.Supplier).Include(p => p.Items).AsSplitQuery().AsQueryable();
        if (fromUtc.HasValue) query = query.Where(p => p.CreatedAt >= fromUtc);
        if (toUtc.HasValue) query = query.Where(p => p.CreatedAt < toUtc);
        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId);
        return PageAsync(query.OrderByDescending(p => p.CreatedAt), page, pageSize, ct);
    }
}
