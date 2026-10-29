using InventoryPOS.Application.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Infrastructure.Persistence;

/// <summary>
/// Copies the register's SQLite file with the online backup API (safe while the app is selling)
/// at startup and then every BackupIntervalHours, keeping the newest BackupKeepCount copies.
/// </summary>
public class SqliteBackupService(IConfiguration config, DatabaseOptions options, ILogger<SqliteBackupService> logger) : BackgroundService, IBackupService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string? BackupDirectory => string.IsNullOrWhiteSpace(options.BackupDirectory) ? null : options.BackupDirectory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (BackupDirectory == null) return;
        var interval = TimeSpan.FromHours(Math.Max(1, options.BackupIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (LastBackupAge() is not { } age || age >= interval - TimeSpan.FromMinutes(5))
                    await BackupNowAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Automatic backup failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    public BackupStatusDto GetStatus() =>
        new(BackupDirectory != null, BackupDirectory, ListBackups().Select(ToDto).ToList());

    async Task<BackupFileDto> IBackupService.BackupNowAsync(CancellationToken ct) => ToDto(new FileInfo(await BackupNowAsync(ct)));

    private static BackupFileDto ToDto(FileInfo f) => new(f.Name, f.Length, f.CreationTimeUtc);

    /// <summary>Creates a backup now and returns its full path.</summary>
    public async Task<string> BackupNowAsync(CancellationToken ct = default)
    {
        var folder = BackupDirectory ?? throw new InvalidOperationException("Database:BackupDirectory is not configured.");
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, $"pos-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            await using (var source = new SqliteConnection(config.GetConnectionString("Sqlite")))
            await using (var destination = new SqliteConnection($"Data Source={target};Pooling=False"))
            {
                await source.OpenAsync(ct);
                await destination.OpenAsync(ct);
                source.BackupDatabase(destination);
            }
            Prune();
            logger.LogInformation("Database backup written to {Path}", target);
            return target;
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<FileInfo> ListBackups() =>
        BackupDirectory == null || !Directory.Exists(BackupDirectory)
            ? []
            : new DirectoryInfo(BackupDirectory).GetFiles("pos-*.db").OrderByDescending(f => f.CreationTimeUtc).ToList();

    private TimeSpan? LastBackupAge()
    {
        var last = ListBackups().FirstOrDefault();
        return last == null ? null : DateTime.UtcNow - last.CreationTimeUtc;
    }

    private void Prune()
    {
        foreach (var old in ListBackups().Skip(Math.Max(1, options.BackupKeepCount)))
        {
            try { old.Delete(); }
            catch (IOException ex) { logger.LogWarning(ex, "Could not delete old backup {File}", old.Name); }
        }
    }
}
