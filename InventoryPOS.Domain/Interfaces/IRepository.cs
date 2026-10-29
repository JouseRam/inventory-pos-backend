using System.Linq.Expressions;
using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Interfaces;

/// <summary>Generic repository over an aggregate; changes are persisted by <see cref="IUnitOfWork"/>.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    void Add(T entity);
    void Remove(T entity);
}
