using System.Security.Cryptography;
using System.Text;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>Central server: pairs registers, hands them the catalog and users, and stores what they sell.</summary>
public class SyncServer(ApplicationDbContext db, SyncMapper mapper, SyncApplier applier, IPosEnvironment env) : ISyncServer
{
    private static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(15);
    /// <summary>Pulls look a little behind their cursor so a change committed during the previous pull is not missed.</summary>
    private static readonly TimeSpan CursorOverlap = TimeSpan.FromMinutes(2);
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<DeviceDto> CreateDeviceAsync(DeviceCreateDto dto, CancellationToken ct = default)
    {
        EnsureCentral();
        var device = new SyncDevice
        {
            Name = dto.Name.Trim(),
            PairingCode = NewCode(),
            PairingExpiresAt = DateTime.UtcNow.Add(PairingLifetime),
        };
        db.SyncDevices.Add(device);
        await db.SaveChangesAsync(ct);
        return ToDto(device);
    }

    public async Task<IReadOnlyList<DeviceDto>> ListDevicesAsync(CancellationToken ct = default) =>
        (await db.SyncDevices.AsNoTracking().OrderByDescending(d => d.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task RevokeDeviceAsync(int id, CancellationToken ct = default)
    {
        var device = await db.SyncDevices.FindAsync([id], ct) ?? throw NotFoundException.For("Caja", id);
        device.IsActive = false;
        device.TokenHash = null;
        device.PairingCode = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Exchanges a one-time pairing code for the device's long-lived token.</summary>
    public async Task<PairResponse> PairAsync(PairRequest request, CancellationToken ct = default)
    {
        EnsureCentral();
        var code = request.Code.Trim().ToUpperInvariant();
        var device = await db.SyncDevices.FirstOrDefaultAsync(d => d.PairingCode == code && d.IsActive, ct);
        if (device == null || device.PairingExpiresAt < DateTime.UtcNow)
            throw new DomainException("El código de vinculación no existe o ya caducó; genera uno nuevo en la web.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        device.TokenHash = Hash(token);
        device.PairingCode = null;
        device.PairingExpiresAt = null;
        device.PairedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.DeviceName)) device.Name = request.DeviceName.Trim();
        await db.SaveChangesAsync(ct);

        var users = await db.Users.AsNoTracking().Select(u => new ServerUserRef(u.Id, u.UserName!)).ToListAsync(ct);
        return new PairResponse(token, device.Name, await db.Products.AnyAsync(ct), users);
    }

    public async Task<int?> AuthenticateAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || env.Role != PosRole.Central) return null;
        var hash = Hash(token);
        var device = await db.SyncDevices.FirstOrDefaultAsync(d => d.TokenHash == hash && d.IsActive, ct);
        if (device == null) return null;
        device.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return device.Id;
    }

    public async Task<PullResponse> PullAsync(int deviceId, DateTime? since, CancellationToken ct = default)
    {
        var cursor = DateTime.UtcNow;
        var from = since?.ToUniversalTime().Subtract(CursorOverlap);
        return new PullResponse(cursor,
            await mapper.BrandsAsync(from, ct), await mapper.CategoriesAsync(from, ct), await mapper.SuppliersAsync(from, ct),
            await mapper.ProductsAsync(from, ct), await mapper.VariantsAsync(from, ct), await mapper.CustomersAsync(from, null, ct),
            await mapper.SettingsAsync(from, ct), await mapper.UsersAsync(from, null, ct));
    }

    /// <summary>Applies a register's batch in one transaction; a failure leaves nothing half-written and the register retries.</summary>
    public async Task<PushResult> PushAsync(int deviceId, PushRequest request, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var serverHadCatalog = await db.Products.AnyAsync(ct);

        // A register's own catalog and accounts are only accepted while the server has none of its own.
        if (request.Users is { Count: > 0 })
            await applier.ApplyUsersAsync(request.Users, overwrite: false, ct);
        if (!serverHadCatalog && request.Products is { Count: > 0 })
            await applier.ApplyCatalogAsync(request.Brands ?? [], request.Categories ?? [], request.Suppliers ?? [],
                request.Products, request.Variants ?? [], overwrite: false, ct);

        var applied = await applier.ApplyOperationsAsync(request, ct);
        var device = await db.SyncDevices.FindAsync([deviceId], ct);
        if (device != null) device.LastPushAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PushResult(applied);
    }

    private void EnsureCentral()
    {
        if (env.Role != PosRole.Central) throw new DomainException("Esta instalación es una caja; las cajas se vinculan en el servidor central.");
    }

    private static string NewCode() =>
        string.Concat(Enumerable.Range(0, 8).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static DeviceDto ToDto(SyncDevice d) =>
        new(d.Id, d.Name, d.TokenHash != null, d.PairingCode, d.PairingExpiresAt, d.LastSeenAt, d.LastPushAt, d.IsActive);
}
