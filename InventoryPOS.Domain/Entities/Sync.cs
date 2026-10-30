using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>
/// Register side: a record changed locally that the central server has not received yet.
/// Holds only a pointer; the payload is read from the current row when sending, so repeated edits collapse.
/// </summary>
public class SyncOutbox : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityUid { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Key/value state of the sync engine (server URL, device credentials, pull cursor, last result).</summary>
public class SyncState : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

/// <summary>Central side: a register allowed to push sales and pull the catalog.</summary>
public class SyncDevice : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>SHA-256 of the device token; the token itself is only shown to the register once.</summary>
    public string? TokenHash { get; set; }
    public string? PairingCode { get; set; }
    public DateTime? PairingExpiresAt { get; set; }
    public DateTime? PairedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastPushAt { get; set; }
    public bool IsActive { get; set; } = true;
}
