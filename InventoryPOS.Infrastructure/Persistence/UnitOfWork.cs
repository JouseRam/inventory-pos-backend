using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Interfaces;
using InventoryPOS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Persistence;

public class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IProductRepository Products => Get(() => new ProductRepository(db));
    public IProductVariantRepository Variants => Get(() => new ProductVariantRepository(db));
    public IProductBatchRepository Batches => Get(() => new ProductBatchRepository(db));
    public IInventoryMovementRepository Movements => Get(() => new InventoryMovementRepository(db));
    public IPurchaseRepository Purchases => Get(() => new PurchaseRepository(db));
    public ISaleRepository Sales => Get(() => new SaleRepository(db));
    public ICashSessionRepository CashSessions => Get(() => new CashSessionRepository(db));

    public IRepository<T> Repository<T>() where T : BaseEntity => Get<IRepository<T>>(() => new Repository<T>(db));

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Otro usuario modificó estos datos al mismo tiempo; vuelve a intentarlo.");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
            return await action();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Pending changes belong to the failed operation; drop them so later saves do not replay them.
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private TRepo Get<TRepo>(Func<TRepo> factory) where TRepo : class
    {
        if (!_repositories.TryGetValue(typeof(TRepo), out var repo))
            _repositories[typeof(TRepo)] = repo = factory();
        return (TRepo)repo;
    }
}
