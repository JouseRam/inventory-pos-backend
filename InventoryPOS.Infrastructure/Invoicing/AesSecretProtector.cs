using System.Security.Cryptography;
using System.Text;
using InventoryPOS.Application.Features.Invoicing;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace InventoryPOS.Infrastructure.Invoicing;

/// <summary>
/// AES-GCM with a key derived from this installation's Jwt:Key, which every installation already keeps out of the
/// repository (user-secrets, the server's web.config, the register's key store). Changing that key makes stored
/// secrets unreadable, and the user enters them again.
/// </summary>
public class AesSecretProtector(IOptions<JwtOptions> jwt) : ISecretProtector
{
    private const string Prefix = "v1:";
    private static readonly byte[] Purpose = "InventoryPOS.Invoicing.Secrets"u8.ToArray();

    private byte[] Key => HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwt.Value.Key), 32, info: Purpose);

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[data.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using (var aes = new AesGcm(Key, tag.Length)) aes.Encrypt(nonce, data, cipher, tag);
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string? Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            var raw = Convert.FromBase64String(protectedValue[Prefix.Length..]);
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            var plain = new byte[raw.Length - nonceSize - tagSize];
            using var aes = new AesGcm(Key, tagSize);
            aes.Decrypt(raw.AsSpan(0, nonceSize), raw.AsSpan(nonceSize + tagSize), raw.AsSpan(nonceSize, tagSize), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
