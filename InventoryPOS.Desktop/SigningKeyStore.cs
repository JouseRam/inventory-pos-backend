using System.Security.Cryptography;

namespace InventoryPOS.Desktop;

/// <summary>
/// The JWT signing key is generated on first run and stored encrypted with Windows DPAPI,
/// so it never lives in a config file and only this Windows account can read it.
/// </summary>
public static class SigningKeyStore
{
    private static readonly byte[] Entropy = "MatizPOS.Jwt.v1"u8.ToArray();

    public static string GetOrCreate(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                return Convert.ToBase64String(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException)
            {
                // Copied from another Windows account or machine: issue a new key; users simply sign in again.
            }
        }

        var key = RandomNumberGenerator.GetBytes(48);
        File.WriteAllBytes(path, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
        return Convert.ToBase64String(key);
    }
}
