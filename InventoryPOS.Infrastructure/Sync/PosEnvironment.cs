using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>Reads the role from Pos:Role (Central by default; the desktop register sets Register).</summary>
public class PosEnvironment(IConfiguration config, ApplicationDbContext db) : IPosEnvironment
{
    public const string RoleKey = "Pos:Role";
    private bool? _paired;

    public PosRole Role => ResolveRole(config);

    public static PosRole ResolveRole(IConfiguration config) =>
        Enum.TryParse<PosRole>(config[RoleKey], ignoreCase: true, out var role) ? role : PosRole.Central;

    public async Task<bool> IsPairedAsync(CancellationToken ct = default) =>
        _paired ??= Role == PosRole.Register &&
            await db.SyncStates.AsNoTracking().AnyAsync(s => s.Key == SyncStateKeys.DeviceToken && s.Value != null && s.Value != "", ct);
}

public static class SyncStateKeys
{
    public const string ServerUrl = "ServerUrl";
    public const string DeviceToken = "DeviceToken";
    public const string DeviceName = "DeviceName";
    public const string PullCursor = "PullCursor";
    public const string LastSuccessAt = "LastSuccessAt";
    public const string LastAttemptAt = "LastAttemptAt";
    public const string LastError = "LastError";
}
