namespace InventoryPOS.Application.Common;

public record BackupFileDto(string Name, long SizeBytes, DateTime CreatedAt);
public record BackupStatusDto(bool Enabled, string? Folder, IReadOnlyList<BackupFileDto> Files);

/// <summary>Copies of the local database; only available when the register runs on SQLite.</summary>
public interface IBackupService
{
    BackupStatusDto GetStatus();
    Task<BackupFileDto> BackupNowAsync(CancellationToken ct = default);
}
