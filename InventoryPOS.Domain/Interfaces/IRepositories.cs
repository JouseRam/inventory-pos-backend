using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;

namespace InventoryPOS.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetWithVariantsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> SearchAsync(string? q, int? categoryId, int? brandId, bool includeInactive, CancellationToken ct = default);
}

public interface IProductVariantRepository : IRepository<ProductVariant>
{
    Task<ProductVariant?> GetWithProductAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductVariant>> GetByIdsWithProductAsync(IEnumerable<int> ids, CancellationToken ct = default);
    /// <summary>Matches name, brand, shade, SKU or barcode among active variants.</summary>
    Task<IReadOnlyList<ProductVariant>> SearchAsync(string q, int take, CancellationToken ct = default);
    Task<ProductVariant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ProductVariant>> GetLowStockAsync(int? take = null, CancellationToken ct = default);
}

public interface IProductBatchRepository : IRepository<ProductBatch>
{
    /// <summary>Batches with stock, earliest expiration first (no expiration last).</summary>
    Task<IReadOnlyList<ProductBatch>> GetAvailableFefoAsync(int variantId, CancellationToken ct = default);
    Task<ProductBatch?> FindByLotAsync(int variantId, string lotNumber, CancellationToken ct = default);
    Task<IReadOnlyList<ProductBatch>> GetExpiringAsync(DateTime untilDate, CancellationToken ct = default);
}

public interface IInventoryMovementRepository : IRepository<InventoryMovement>
{
    Task<PagedResult<InventoryMovement>> GetPagedAsync(int? variantId, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryMovement>> GetByReferenceAsync(string reference, CancellationToken ct = default);
}

public interface IPurchaseRepository : IRepository<Purchase>
{
    Task<Purchase?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Purchase>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, int? supplierId, int page, int pageSize, CancellationToken ct = default);
}

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithDetailsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Sale>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, string? paymentMethod, int? customerId, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Payments of non-cancelled sales registered in a cash session.</summary>
    Task<IReadOnlyList<SalePayment>> GetSessionPaymentsAsync(int cashSessionId, CancellationToken ct = default);
}

public interface ICashSessionRepository : IRepository<CashSession>
{
    Task<CashSession?> GetOpenByUserAsync(string userId, CancellationToken ct = default);
    Task<CashSession?> GetWithMovementsAsync(int id, CancellationToken ct = default);
    Task<PagedResult<CashSession>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default);
}
