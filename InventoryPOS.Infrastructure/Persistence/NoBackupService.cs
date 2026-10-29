using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Common;

namespace InventoryPOS.Infrastructure.Persistence;

/// <summary>SQL Server deployments back up through the database server, not the app.</summary>
public class NoBackupService : IBackupService
{
    public BackupStatusDto GetStatus() => new(false, null, []);

    public Task<BackupFileDto> BackupNowAsync(CancellationToken ct = default) =>
        throw new DomainException("Los respaldos automáticos solo aplican a la caja de escritorio (SQLite).");
}
