using System.Globalization;
using System.Net.Http.Json;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Infrastructure.Sync;

/// <summary>
/// Register side. Each round first pushes the outbox in batches (oldest first), then pulls what the server changed.
/// Sends are idempotent on the server, so a round cut by a lost connection is simply repeated.
/// </summary>
public class SyncClient(
    ApplicationDbContext db, SyncMapper mapper, SyncApplier applier, IPosEnvironment env, IHttpClientFactory http, ILogger<SyncClient> logger)
    : ISyncClient
{
    public const string HttpClientName = "sync";
    private const string TokenHeader = "X-Device-Token";
    private const string UploadCatalogKey = "UploadCatalog";
    private const int BatchSize = 300;
    /// <summary>One round at a time per process (the background worker and the "Sync now" button share it).</summary>
    private static readonly SemaphoreSlim Round = new(1, 1);

    public async Task<SyncStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var state = await StateAsync(ct);
        var pending = env.Role == PosRole.Register
            ? await db.SyncOutbox.Select(o => new { o.EntityType, o.EntityUid }).Distinct().CountAsync(ct)
            : 0;
        return new SyncStatusDto(env.Role.ToString(), env.Role == PosRole.Register && !string.IsNullOrEmpty(state.GetValueOrDefault(SyncStateKeys.DeviceToken)),
            state.GetValueOrDefault(SyncStateKeys.ServerUrl), state.GetValueOrDefault(SyncStateKeys.DeviceName), pending,
            ParseDate(state.GetValueOrDefault(SyncStateKeys.LastSuccessAt)), ParseDate(state.GetValueOrDefault(SyncStateKeys.LastAttemptAt)),
            state.GetValueOrDefault(SyncStateKeys.LastError));
    }

    public async Task<SyncStatusDto> PairAsync(PairDeviceDto dto, CancellationToken ct = default)
    {
        if (env.Role != PosRole.Register) throw new DomainException("Solo una caja se vincula con el servidor.");
        if (await IsPairedNowAsync(ct)) throw new DomainException("Esta caja ya está vinculada.");
        var serverUrl = NormalizeUrl(dto.ServerUrl);

        PairResponse pair;
        try
        {
            var response = await Client(serverUrl).PostAsJsonAsync("api/sync/pair", new PairRequest(dto.Code, Environment.MachineName), ct);
            if (!response.IsSuccessStatusCode) throw new DomainException(await ProblemAsync(response, ct));
            pair = (await response.Content.ReadFromJsonAsync<PairResponse>(cancellationToken: ct))!;
        }
        catch (HttpRequestException ex)
        {
            throw new DomainException($"No se pudo contactar al servidor {serverUrl}: {ex.Message}");
        }

        var localHasCatalog = await db.Products.AnyAsync(ct);
        if (localHasCatalog && pair.ServerHasCatalog)
            throw new DomainException("La caja y la web ya tienen productos dados de alta. Deja el catálogo en un solo lado (normalmente la web) y vuelve a vincular.");

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await AdoptServerAccountsAsync(pair.Users, ct);
            await EnqueueEverythingAsync(ct);
            await SetStateAsync(SyncStateKeys.ServerUrl, serverUrl, ct);
            await SetStateAsync(SyncStateKeys.DeviceToken, pair.DeviceToken, ct);
            await SetStateAsync(SyncStateKeys.DeviceName, pair.DeviceName, ct);
            await SetStateAsync(SyncStateKeys.PullCursor, null, ct);
            await SetStateAsync(UploadCatalogKey, localHasCatalog ? "true" : null, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        logger.LogInformation("Register paired with {Server} as {Device}", serverUrl, pair.DeviceName);
        return await SyncNowAsync(ct);
    }

    public async Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default)
    {
        if (env.Role != PosRole.Register || !await IsPairedNowAsync(ct)) return await GetStatusAsync(ct);
        await Round.WaitAsync(ct);
        try
        {
            var state = await StateAsync(ct);
            var client = Client(state[SyncStateKeys.ServerUrl]!, state[SyncStateKeys.DeviceToken]);
            await SetStateAsync(SyncStateKeys.LastAttemptAt, DateTime.UtcNow.ToString("O"), ct);
            await db.SaveChangesAsync(ct);

            await PushAllAsync(client, uploadCatalog: state.GetValueOrDefault(UploadCatalogKey) == "true", ct);
            await PullAsync(client, ParseDate(state.GetValueOrDefault(SyncStateKeys.PullCursor)), ct);

            await SetStateAsync(SyncStateKeys.LastSuccessAt, DateTime.UtcNow.ToString("O"), ct);
            await SetStateAsync(SyncStateKeys.LastError, null, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or DomainException or InvalidOperationException)
        {
            db.ChangeTracker.Clear();
            var message = ex switch
            {
                HttpRequestException or TaskCanceledException => "Sin conexión con el servidor; se reintentará solo.",
                _ => ex.Message,
            };
            logger.LogWarning(ex, "Sync round failed");
            await SetStateAsync(SyncStateKeys.LastError, message, ct);
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            Round.Release();
        }
        return await GetStatusAsync(ct);
    }

    // ── Push ──

    private async Task PushAllAsync(HttpClient client, bool uploadCatalog, CancellationToken ct)
    {
        while (true)
        {
            var rows = await db.SyncOutbox.AsNoTracking().OrderBy(o => o.Id).Take(BatchSize).ToListAsync(ct);
            if (rows.Count == 0 && !uploadCatalog) return;

            var request = await BuildPushAsync(rows, uploadCatalog, ct);
            var response = await client.PostAsJsonAsync("api/sync/push", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var problem = await ProblemAsync(response, ct);
                var ids = rows.Select(r => r.Id).ToList();
                await db.SyncOutbox.Where(o => ids.Contains(o.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1).SetProperty(o => o.LastError, problem), ct);
                throw new DomainException($"El servidor rechazó el envío: {problem}");
            }

            // Rows added while this batch was in flight have higher ids and stay for the next one.
            var sent = rows.Select(r => r.Id).ToList();
            await db.SyncOutbox.Where(o => sent.Contains(o.Id)).ExecuteDeleteAsync(ct);
            if (uploadCatalog)
            {
                uploadCatalog = false;
                await SetStateAsync(UploadCatalogKey, null, ct);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// Builds one request from outbox rows. Referenced records (a sale's cash session and customer, a movement's lot)
    /// travel along so the server never receives a reference it cannot resolve.
    /// </summary>
    private async Task<PushRequest> BuildPushAsync(IReadOnlyList<SyncOutbox> rows, bool uploadCatalog, CancellationToken ct)
    {
        HashSet<Guid> Of(string type) => rows.Where(r => r.EntityType == type).Select(r => r.EntityUid).ToHashSet();

        var sales = await mapper.SalesAsync(Of(SyncTypes.Sale), ct);
        var movements = await mapper.MovementsAsync(Of(SyncTypes.Movement), ct);
        var purchases = await mapper.PurchasesAsync(Of(SyncTypes.Purchase), ct);

        var sessionUids = Of(SyncTypes.CashSession);
        sessionUids.UnionWith(sales.Select(s => s.CashSessionUid));
        var customerUids = Of(SyncTypes.Customer);
        customerUids.UnionWith(sales.Where(s => s.CustomerUid.HasValue).Select(s => s.CustomerUid!.Value));
        var batchUids = Of(SyncTypes.Batch);
        batchUids.UnionWith(movements.Where(m => m.BatchUid.HasValue).Select(m => m.BatchUid!.Value));
        batchUids.UnionWith(purchases.SelectMany(p => p.Items).Where(i => i.BatchUid.HasValue).Select(i => i.BatchUid!.Value));

        var userIds = sales.Select(s => s.UserId).Concat(movements.Select(m => m.UserId)).Concat(purchases.Select(p => p.UserId))
            .Where(id => id != null).Select(id => id!).ToHashSet();

        return new PushRequest(
            await mapper.CustomersAsync(null, customerUids, ct), await mapper.BatchesAsync(batchUids, ct), purchases,
            await mapper.CashSessionsAsync(sessionUids, ct), sales, movements, await mapper.StocksAsync(Of(SyncTypes.Stock), ct),
            uploadCatalog ? await mapper.BrandsAsync(null, ct) : null,
            uploadCatalog ? await mapper.CategoriesAsync(null, ct) : null,
            uploadCatalog ? await mapper.SuppliersAsync(null, ct) : null,
            uploadCatalog ? await mapper.ProductsAsync(null, ct) : null,
            uploadCatalog ? await mapper.VariantsAsync(null, ct) : null,
            // Accounts referenced by operations (and all local accounts on the first upload) so the server can attribute them.
            uploadCatalog ? await mapper.UsersAsync(null, null, ct) : userIds.Count > 0 ? await mapper.UsersAsync(null, userIds, ct) : null);
    }

    // ── Pull ──

    private async Task PullAsync(HttpClient client, DateTime? cursor, CancellationToken ct)
    {
        var url = "api/sync/pull" + (cursor.HasValue ? "?since=" + Uri.EscapeDataString(cursor.Value.ToString("O")) : "");
        var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) throw new DomainException($"No se pudieron bajar los cambios: {await ProblemAsync(response, ct)}");
        var pull = (await response.Content.ReadFromJsonAsync<PullResponse>(cancellationToken: ct))!;

        SyncSaveChangesInterceptor.SuppressOutbox.Value = true;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await applier.ApplyUsersAsync(pull.Users, overwrite: true, ct);
            await applier.ApplyCatalogAsync(pull.Brands, pull.Categories, pull.Suppliers, pull.Products, pull.Variants, overwrite: true, ct);
            await applier.ApplyCustomersAsync(pull.Customers, ct);
            await applier.ApplySettingsAsync(pull.Settings, ct);
            await SetStateAsync(SyncStateKeys.PullCursor, pull.Cursor.ToString("O"), ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        finally
        {
            SyncSaveChangesInterceptor.SuppressOutbox.Value = false;
        }
    }

    // ── Pairing helpers ──

    /// <summary>
    /// A local account whose user name exists on the server becomes that server account: its sales, sessions and
    /// movements are re-pointed to the server id, so the history stays attributed after the server's users arrive.
    /// </summary>
    private async Task AdoptServerAccountsAsync(IReadOnlyList<ServerUserRef> serverUsers, CancellationToken ct)
    {
        var byName = serverUsers.ToDictionary(u => u.UserName.ToUpperInvariant(), u => u.Id);
        var locals = await db.Users.AsNoTracking().Select(u => new { u.Id, u.UserName, u.NormalizedUserName }).ToListAsync(ct);
        foreach (var local in locals)
        {
            if (!byName.TryGetValue(local.NormalizedUserName ?? "", out var serverId) || serverId == local.Id) continue;

            // Temporary name frees the unique user name while both rows exist.
            var original = await db.Users.FirstAsync(u => u.Id == local.Id, ct);
            var copy = new Identity.AppUser
            {
                Id = serverId, UserName = "__sync_" + serverId, NormalizedUserName = "__SYNC_" + serverId.ToUpperInvariant(),
                FullName = original.FullName, PasswordHash = original.PasswordHash, SecurityStamp = original.SecurityStamp,
                IsSuperAdmin = original.IsSuperAdmin, IsActive = original.IsActive,
            };
            db.Users.Add(copy);
            await db.SaveChangesAsync(ct);

            await db.CashSessions.Where(x => x.UserId == local.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, serverId), ct);
            await db.CashMovements.Where(x => x.UserId == local.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, serverId), ct);
            await db.Sales.Where(x => x.UserId == local.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, serverId), ct);
            await db.Purchases.Where(x => x.UserId == local.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, serverId), ct);
            await db.InventoryMovements.Where(x => x.UserId == local.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, serverId), ct);
            await db.UserModulePermissions.Where(x => x.UserId == local.Id).ExecuteDeleteAsync(ct);

            db.Users.Remove(original);
            await db.SaveChangesAsync(ct);
            copy.UserName = local.UserName;
            copy.NormalizedUserName = local.NormalizedUserName;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Queues the register's whole history so the server receives everything sold before pairing.</summary>
    private async Task EnqueueEverythingAsync(CancellationToken ct)
    {
        async Task Add<T>(string type) where T : BaseEntity
        {
            foreach (var uid in await db.Set<T>().AsNoTracking().OrderBy(e => e.Id).Select(e => e.Uid).ToListAsync(ct))
                db.SyncOutbox.Add(new SyncOutbox { EntityType = type, EntityUid = uid });
            await db.SaveChangesAsync(ct);
        }
        await db.SyncOutbox.ExecuteDeleteAsync(ct);
        await Add<Customer>(SyncTypes.Customer);
        await Add<ProductBatch>(SyncTypes.Batch);
        await Add<Purchase>(SyncTypes.Purchase);
        await Add<CashSession>(SyncTypes.CashSession);
        await Add<Sale>(SyncTypes.Sale);
        await Add<InventoryMovement>(SyncTypes.Movement);
        await Add<ProductVariant>(SyncTypes.Stock);
    }

    // ── State ──

    /// <summary>Read straight from the table: the environment caches the answer per request, and pairing changes it mid-request.</summary>
    private Task<bool> IsPairedNowAsync(CancellationToken ct) =>
        db.SyncStates.AnyAsync(s => s.Key == SyncStateKeys.DeviceToken && s.Value != null && s.Value != "", ct);

    private async Task<Dictionary<string, string?>> StateAsync(CancellationToken ct) =>
        await db.SyncStates.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    private async Task SetStateAsync(string key, string? value, CancellationToken ct)
    {
        var row = db.SyncStates.Local.FirstOrDefault(s => s.Key == key) ?? await db.SyncStates.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row == null) db.SyncStates.Add(new SyncState { Key = key, Value = value });
        else row.Value = value;
    }

    private HttpClient Client(string serverUrl, string? token = null)
    {
        var client = http.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(serverUrl);
        if (token != null) client.DefaultRequestHeaders.Add(TokenHeader, token);
        return client;
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "https://" + trimmed;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) throw new DomainException("La dirección del servidor no es válida.");
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
    }

    private static async Task<string> ProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct);
            if (problem?.GetValueOrDefault("detail")?.ToString() is { Length: > 0 } detail) return detail;
        }
        catch (System.Text.Json.JsonException) { }
        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : null;
}
