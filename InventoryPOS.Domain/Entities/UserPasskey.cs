using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>
/// A passkey (WebAuthn credential) a user registered on a device to sign in with Face ID, Touch ID, fingerprint or Windows Hello.
/// Only the public key is stored: the biometric never leaves the device, which signs a fresh challenge at each sign-in.
/// </summary>
public class UserPasskey : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    /// <summary>Signature counter reported by the device; a counter that goes back reveals a cloned credential.</summary>
    public long SignCount { get; set; }
    /// <summary>Name the user sees to tell their devices apart ("iPhone", "Windows").</summary>
    public string Name { get; set; } = string.Empty;
    public Guid AaGuid { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
