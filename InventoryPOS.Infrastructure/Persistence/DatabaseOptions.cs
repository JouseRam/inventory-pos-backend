namespace InventoryPOS.Infrastructure.Persistence;

/// <summary>
/// "Database" configuration section. The desktop register runs on SQLite (a single local file);
/// development and any future central server can keep using SQL Server.
/// </summary>
public class DatabaseOptions
{
    public const string Section = "Database";
    public const string SqliteMigrationsAssembly = "InventoryPOS.Infrastructure.Sqlite";

    /// <summary>SqlServer | Sqlite</summary>
    public string Provider { get; set; } = "SqlServer";
    /// <summary>Apply pending migrations at startup (always on for the desktop register).</summary>
    public bool AutoMigrate { get; set; }
    /// <summary>Folder for automatic SQLite backups; empty disables them.</summary>
    public string? BackupDirectory { get; set; }
    public int BackupKeepCount { get; set; } = 30;
    public int BackupIntervalHours { get; set; } = 24;

    public bool IsSqlite => string.Equals(Provider, "Sqlite", StringComparison.OrdinalIgnoreCase);
}
