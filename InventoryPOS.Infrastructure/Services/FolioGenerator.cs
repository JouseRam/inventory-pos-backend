using InventoryPOS.Application.Common;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Services;

/// <summary>
/// Increments the series row with an UPDATE, which locks it until the surrounding transaction ends,
/// so concurrent callers queue up and never receive the same number (unlike Count()+1).
/// </summary>
public class FolioGenerator(ApplicationDbContext db) : IFolioGenerator
{
    public async Task<string> NextAsync(string series, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("FolioGenerator.NextAsync must run inside a transaction.");

        var updated = await db.FolioSequences.Where(f => f.Name == series)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.NextValue, f => f.NextValue + 1), ct);
        if (updated == 0)
            throw new InvalidOperationException($"Folio series '{series}' is not configured.");

        var sequence = await db.FolioSequences.AsNoTracking()
            .Where(f => f.Name == series)
            .Select(f => new { f.Prefix, f.NextValue })
            .FirstAsync(ct);
        return $"{sequence.Prefix}{sequence.NextValue - 1:D6}";
    }
}
