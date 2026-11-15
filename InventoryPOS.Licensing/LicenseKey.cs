using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryPOS.Licensing;

/// <summary>What an activation key grants: to whom, on which computer, since when and until when (if it expires).</summary>
public sealed record LicenseInfo(
    [property: JsonPropertyName("c")] string Customer,
    [property: JsonPropertyName("m")] string Machine,
    [property: JsonPropertyName("i")] DateOnly IssuedOn,
    [property: JsonPropertyName("e")] DateOnly? ExpiresOn,
    [property: JsonPropertyName("n")] string Id);

public enum LicenseStatus { Valid, Missing, Malformed, BadSignature, OtherComputer, Expired }

public sealed record LicenseCheck(LicenseStatus Status, LicenseInfo? License)
{
    public bool IsValid => Status == LicenseStatus.Valid;

    /// <summary>What to tell the person at the register.</summary>
    public string Message => Status switch
    {
        LicenseStatus.Valid => $"Activado para {License!.Customer}.",
        LicenseStatus.Missing => "Este equipo aún no está activado.",
        LicenseStatus.Malformed => "La clave de activación está incompleta o mal copiada.",
        LicenseStatus.BadSignature => "La clave de activación no es válida.",
        LicenseStatus.OtherComputer => "La clave de activación es de otra computadora. Cada computadora necesita su propia clave.",
        LicenseStatus.Expired => $"La licencia venció el {License!.ExpiresOn:dd/MM/yyyy}.",
        _ => "Licencia no válida.",
    };
}

/// <summary>
/// Activation keys: the license data signed with the vendor's private key (ECDSA P-256). The program carries only the public key,
/// so it can check a key but never make one. Text form: "ABS1.&lt;data&gt;.&lt;signature&gt;" (base64url).
/// </summary>
public static class LicenseKey
{
    private const string Prefix = "ABS1";

    /// <summary>A new vendor key pair: the private one (PKCS#8, keep it secret) and the public one to put in the program.</summary>
    public static (string PrivateKey, string PublicKey) CreateKeyPair()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (Convert.ToBase64String(ec.ExportPkcs8PrivateKey()), Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo()));
    }

    /// <summary>Public key that belongs to a private key (to check which program a private key is for).</summary>
    public static string PublicKeyOf(string privateKey)
    {
        using var ec = ECDsa.Create();
        ec.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey.Trim()), out _);
        return Convert.ToBase64String(ec.ExportSubjectPublicKeyInfo());
    }

    public static string Issue(string privateKey, string customer, MachineCode machine, DateOnly issuedOn, DateOnly? expiresOn = null)
    {
        if (string.IsNullOrWhiteSpace(customer)) throw new ArgumentException("Falta el nombre del cliente.", nameof(customer));
        var info = new LicenseInfo(customer.Trim(), machine.ToString(), issuedOn, expiresOn, Convert.ToHexString(RandomNumberGenerator.GetBytes(6)));
        var data = JsonSerializer.SerializeToUtf8Bytes(info);
        using var ec = ECDsa.Create();
        ec.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey.Trim()), out _);
        var signature = ec.SignData(Signed(data), HashAlgorithmName.SHA256);
        return $"{Prefix}.{Base64Url(data)}.{Base64Url(signature)}";
    }

    /// <summary>Checks a key against the vendor's public key, this computer and today's date.</summary>
    public static LicenseCheck Check(string? key, string publicKey, MachineCode thisComputer, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(key)) return new(LicenseStatus.Missing, null);
        var parts = string.Concat(key.Where(c => !char.IsWhiteSpace(c))).Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return new(LicenseStatus.Malformed, null);

        byte[] data, signature;
        LicenseInfo? info;
        try
        {
            data = FromBase64Url(parts[1]);
            signature = FromBase64Url(parts[2]);
            info = JsonSerializer.Deserialize<LicenseInfo>(data);
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { return new(LicenseStatus.Malformed, null); }
        if (info is null) return new(LicenseStatus.Malformed, null);

        using (var ec = ECDsa.Create())
        {
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            if (!ec.VerifyData(Signed(data), signature, HashAlgorithmName.SHA256)) return new(LicenseStatus.BadSignature, null);
        }
        if (!MachineCode.TryParse(info.Machine, out var licensed) || !thisComputer.Matches(licensed)) return new(LicenseStatus.OtherComputer, info);
        if (info.ExpiresOn is { } expires && today > expires) return new(LicenseStatus.Expired, info);
        return new(LicenseStatus.Valid, info);
    }

    // The prefix is signed too, so a key for another product or format version is never accepted.
    private static byte[] Signed(byte[] data) => [.. Encoding.ASCII.GetBytes(Prefix + "."), .. data];

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
