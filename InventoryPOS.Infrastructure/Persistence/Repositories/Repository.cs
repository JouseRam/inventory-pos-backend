using System.Linq.Expressions;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Persistence.Repositories;

public class Repository<T>(ApplicationDbContext db) : IRepository<T> where T : BaseEntity
{
    protected ApplicationDbContext Db { get; } = db;
    protected DbSet<T> Set => Db.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate == null ? await Set.ToListAsync(ct) : await Set.Where(predicate).ToListAsync(ct);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(predicate, ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate == null ? Set.CountAsync(ct) : Set.CountAsync(predicate, ct);

    public void Add(T entity) => Set.Add(entity);

    public void Remove(T entity) => Set.Remove(entity);

    protected static async Task<PagedResult<T>> PageAsync(IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(data, total, page, pageSize);
    }
}
