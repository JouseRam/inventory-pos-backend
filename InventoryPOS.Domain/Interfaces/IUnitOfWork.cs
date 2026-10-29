using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Interfaces;

public interface IUnitOfWork
{
    IProductRepository Products { get; }
    IProductVariantRepository Variants { get; }
    IProductBatchRepository Batches { get; }
    IInventoryMovementRepository Movements { get; }
    IPurchaseRepository Purchases { get; }
    ISaleRepository Sales { get; }
    ICashSessionRepository CashSessions { get; }

    /// <summary>Repository for entities without specific queries (catalogs, settings, permissions).</summary>
    IRepository<T> Repository<T>() where T : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs the action inside a database transaction; commits on success, rolls back on any exception.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
