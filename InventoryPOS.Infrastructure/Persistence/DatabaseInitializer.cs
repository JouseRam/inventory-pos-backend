using InventoryPOS.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Infrastructure.Persistence;

/// <summary>Brings the database to the current schema and seeds the defaults the store needs.</summary>
public class DatabaseInitializer(ApplicationDbContext db, DbSeeder seeder, DatabaseOptions options, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool migrate, CancellationToken ct = default)
    {
        if (options.IsSqlite)
        {
            var path = db.Database.GetDbConnection().DataSource;
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        }

        if (migrate || options.AutoMigrate)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync(ct);
            }
        }

        // Write-ahead logging keeps the file consistent if the register loses power mid-sale.
        if (options.IsSqlite)
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        await seeder.SeedAsync(ct);
    }
}
