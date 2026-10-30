using FluentValidation;

namespace InventoryPOS.Application.Features.Sync;

/// <summary>Outbox aggregate names; each is sent as one record with its children.</summary>
public static class SyncTypes
{
    public const string Customer = "Customer";
    public const string Sale = "Sale";
    public const string CashSession = "CashSession";
    public const string Purchase = "Purchase";
    public const string Batch = "Batch";
    public const string Movement = "Movement";
    public const string Stock = "Stock";
}

// ── Records as they travel. References use the global Uid; user ids are the same on both sides. ──

public record BrandSync(Guid Uid, string Name, bool IsActive);
public record CategorySync(Guid Uid, string Name, string? Description, Guid? ParentUid, bool IsActive);
public record SupplierSync(Guid Uid, string Name, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive);
public record CustomerSync(Guid Uid, string Name, string? Phone, string? Email, string? Address, string? Notes, bool IsActive,
    DateTime CreatedAt, DateTime? UpdatedAt);
public record ProductSync(Guid Uid, string Name, string? Description, Guid? BrandUid, Guid? CategoryUid, Guid? SupplierUid,
    bool TracksBatches, bool IsActive);
/// <summary>Catalog data of a variant; stock is owned by the register and travels separately.</summary>
public record VariantSync(Guid Uid, Guid ProductUid, string SKU, string? Barcode, string? ShadeCode, string? ShadeName, decimal? Size,
    string Unit, decimal Price, decimal Cost, decimal MinStock, bool IsActive);
public record SettingSync(string Key, string? Value, string Label, string Type, string Group);
public record PermissionSync(string Module, bool CanView, bool CanEdit);
/// <summary>Accounts with their password hash so cashiers can sign in at the register without internet.</summary>
public record UserSync(string Id, string UserName, string? Email, string FullName, bool IsSuperAdmin, bool IsActive,
    string? PasswordHash, string? SecurityStamp, IReadOnlyList<PermissionSync> Permissions);

public record BatchSync(Guid Uid, Guid VariantUid, string LotNumber, DateTime? ExpirationDate, decimal Quantity, DateTime CreatedAt);
public record MovementSync(Guid Uid, Guid VariantUid, Guid? BatchUid, string Type, decimal Quantity, decimal StockAfter, decimal UnitCost,
    string? Reference, string? Notes, string? UserId, DateTime CreatedAt);
public record PurchaseItemSync(Guid Uid, Guid VariantUid, decimal Quantity, decimal UnitCost, decimal Subtotal, string? LotNumber,
    DateTime? ExpirationDate, Guid? BatchUid);
public record PurchaseSync(Guid Uid, string Folio, Guid? SupplierUid, string? InvoiceNumber, decimal Total, string? Notes, string? UserId,
    DateTime CreatedAt, IReadOnlyList<PurchaseItemSync> Items);
public record CashMovementSync(Guid Uid, string Type, decimal Amount, string Reason, string? UserId, DateTime CreatedAt);
public record CashSessionSync(Guid Uid, string Folio, string UserId, string Status, decimal OpeningAmount, DateTime OpenedAt, DateTime? ClosedAt,
    decimal? ExpectedCash, decimal? CountedCash, decimal? Difference, string? Notes, IReadOnlyList<CashMovementSync> Movements);
public record SaleItemSync(Guid Uid, Guid VariantUid, string Description, decimal Quantity, decimal UnitPrice, decimal UnitCost,
    decimal Discount, decimal Subtotal);
public record SalePaymentSync(Guid Uid, string Method, decimal Amount, string? Reference);
public record SaleSync(Guid Uid, string Folio, Guid? CustomerUid, Guid CashSessionUid, decimal Subtotal, decimal Discount, decimal Tax,
    decimal Total, decimal AmountPaid, decimal Change, string Status, string? Notes, string? CancelReason, DateTime? CancelledAt,
    string? UserId, DateTime CreatedAt, IReadOnlyList<SaleItemSync> Items, IReadOnlyList<SalePaymentSync> Payments);
public record StockSync(Guid VariantUid, decimal Stock);

/// <summary>Central → register: everything administered on the web that changed since the cursor.</summary>
public record PullResponse(
    DateTime Cursor,
    IReadOnlyList<BrandSync> Brands, IReadOnlyList<CategorySync> Categories, IReadOnlyList<SupplierSync> Suppliers,
    IReadOnlyList<ProductSync> Products, IReadOnlyList<VariantSync> Variants, IReadOnlyList<CustomerSync> Customers,
    IReadOnlyList<SettingSync> Settings, IReadOnlyList<UserSync> Users);

/// <summary>
/// Register → central. Operations always travel; catalog and users only on the first upload after pairing,
/// when the register had its own catalog and the server had none.
/// </summary>
public record PushRequest(
    IReadOnlyList<CustomerSync> Customers, IReadOnlyList<BatchSync> Batches, IReadOnlyList<PurchaseSync> Purchases,
    IReadOnlyList<CashSessionSync> CashSessions, IReadOnlyList<SaleSync> Sales, IReadOnlyList<MovementSync> Movements,
    IReadOnlyList<StockSync> Stocks,
    IReadOnlyList<BrandSync>? Brands = null, IReadOnlyList<CategorySync>? Categories = null, IReadOnlyList<SupplierSync>? Suppliers = null,
    IReadOnlyList<ProductSync>? Products = null, IReadOnlyList<VariantSync>? Variants = null, IReadOnlyList<UserSync>? Users = null);

public record PushResult(int Applied);

public record PairRequest(string Code, string DeviceName);
/// <summary>What a register needs to decide how to merge: its credential and whether the server already has data.</summary>
public record PairResponse(string DeviceToken, string DeviceName, bool ServerHasCatalog, IReadOnlyList<ServerUserRef> Users);
public record ServerUserRef(string Id, string UserName);

// ── Screens ──

public record SyncStatusDto(string Role, bool Paired, string? ServerUrl, string? DeviceName, int Pending,
    DateTime? LastSuccessAt, DateTime? LastAttemptAt, string? LastError);
public record PairDeviceDto(string ServerUrl, string Code);
public record DeviceDto(int Id, string Name, bool Paired, string? PairingCode, DateTime? PairingExpiresAt, DateTime? LastSeenAt,
    DateTime? LastPushAt, bool IsActive);
public record DeviceCreateDto(string Name);

/// <summary>Central server side of the protocol.</summary>
public interface ISyncServer
{
    Task<DeviceDto> CreateDeviceAsync(DeviceCreateDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceDto>> ListDevicesAsync(CancellationToken ct = default);
    Task RevokeDeviceAsync(int id, CancellationToken ct = default);
    Task<PairResponse> PairAsync(PairRequest request, CancellationToken ct = default);
    /// <summary>Validates a device token; returns the device id or null.</summary>
    Task<int?> AuthenticateAsync(string? token, CancellationToken ct = default);
    Task<PullResponse> PullAsync(int deviceId, DateTime? since, CancellationToken ct = default);
    Task<PushResult> PushAsync(int deviceId, PushRequest request, CancellationToken ct = default);
}

/// <summary>Register side: pairing, status and one sync round (push pending, then pull changes).</summary>
public interface ISyncClient
{
    Task<SyncStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<SyncStatusDto> PairAsync(PairDeviceDto dto, CancellationToken ct = default);
    Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default);
}

public class PairDeviceValidator : AbstractValidator<PairDeviceDto>
{
    public PairDeviceValidator()
    {
        RuleFor(x => x.ServerUrl).NotEmpty().WithMessage("Captura la dirección del servidor.").MaximumLength(300);
        RuleFor(x => x.Code).NotEmpty().WithMessage("Captura el código de vinculación.").Length(8).WithMessage("El código tiene 8 caracteres.");
    }
}

public class DeviceCreateValidator : AbstractValidator<DeviceCreateDto>
{
    public DeviceCreateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ponle nombre a la caja.").MaximumLength(100);
    }
}
