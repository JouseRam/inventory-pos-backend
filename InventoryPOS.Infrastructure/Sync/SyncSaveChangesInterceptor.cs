using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>
/// Stamps UpdatedAt on every change (the central server pulls by it) and, on a register, records each
/// register-owned change in the outbox inside the same SaveChanges, so a sale and its pending upload commit together.
/// </summary>
public class SyncSaveChangesInterceptor(PosRole role) : SaveChangesInterceptor
{
    /// <summary>Set while applying records pulled from the server, so they are not queued to be sent back.</summary>
    public static readonly AsyncLocal<bool> SuppressOutbox = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null) Track(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) Track(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Track(DbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;
        var pending = new HashSet<(string Type, Guid Uid)>();

        foreach (var entry in db.ChangeTracker.Entries<BaseEntity>().ToList())
        {
            if (entry.Entity is SyncOutbox or SyncState) continue;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = now;

            if (role == PosRole.Register && !SuppressOutbox.Value && OutboxKey(entry) is { } key)
                pending.Add(key);
        }

        foreach (var (type, uid) in pending)
            db.Set<SyncOutbox>().Add(new SyncOutbox { EntityType = type, EntityUid = uid });
    }

    /// <summary>The aggregate a change belongs to, as the central server receives it.</summary>
    private static (string, Guid)? OutboxKey(EntityEntry<BaseEntity> entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified)) return null;
        return entry.Entity switch
        {
            Customer c => (SyncTypes.Customer, c.Uid),
            Sale s => (SyncTypes.Sale, s.Uid),
            CashSession s => (SyncTypes.CashSession, s.Uid),
            CashMovement m when m.CashSession != null => (SyncTypes.CashSession, m.CashSession.Uid),
            Purchase p => (SyncTypes.Purchase, p.Uid),
            ProductBatch b => (SyncTypes.Batch, b.Uid),
            InventoryMovement m => (SyncTypes.Movement, m.Uid),
            ProductVariant v when entry.State == EntityState.Added || entry.Property(nameof(ProductVariant.Stock)).IsModified
                => (SyncTypes.Stock, v.Uid),
            _ => null,
        };
    }
}
