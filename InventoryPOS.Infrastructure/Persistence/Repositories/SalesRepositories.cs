using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Persistence.Repositories;

public class SaleRepository(ApplicationDbContext db) : Repository<Sale>(db), ISaleRepository
{
    public Task<Sale?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Set.Include(s => s.Customer).Include(s => s.Items).Include(s => s.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<PagedResult<Sale>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, string? paymentMethod, int? customerId, int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(s => s.Customer).Include(s => s.Items).Include(s => s.Payments).AsSplitQuery().AsQueryable();
        if (fromUtc.HasValue) query = query.Where(s => s.CreatedAt >= fromUtc);
        if (toUtc.HasValue) query = query.Where(s => s.CreatedAt < toUtc);
        if (!string.IsNullOrEmpty(paymentMethod)) query = query.Where(s => s.Payments.Any(p => p.Method == paymentMethod));
        if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId);
        return PageAsync(query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id), page, pageSize, ct);
    }

    public async Task<IReadOnlyList<SalePayment>> GetSessionPaymentsAsync(int cashSessionId, CancellationToken ct = default) =>
        await Db.SalePayments.AsNoTracking()
            .Where(p => p.Sale.CashSessionId == cashSessionId && p.Sale.Status != SaleStatus.Cancelled)
            .ToListAsync(ct);
}

public class CashSessionRepository(ApplicationDbContext db) : Repository<CashSession>(db), ICashSessionRepository
{
    public Task<CashSession?> GetOpenByUserAsync(string userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashSessionStatus.Open, ct);

    public Task<CashSession?> GetWithMovementsAsync(int id, CancellationToken ct = default) =>
        Set.Include(s => s.Movements).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<PagedResult<CashSession>> GetPagedAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking();
        if (fromUtc.HasValue) query = query.Where(s => s.OpenedAt >= fromUtc);
        if (toUtc.HasValue) query = query.Where(s => s.OpenedAt < toUtc);
        return PageAsync(query.OrderByDescending(s => s.OpenedAt), page, pageSize, ct);
    }
}
