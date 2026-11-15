using System.Security.Cryptography;
using System.Text;

namespace InventoryPOS.Licensing;

/// <summary>
/// Identifies one computer by three hardware traits (Windows installation id, board UUID, board serial), each reduced to a short
/// hash. The customer reads it as 28 characters ("ABCD-EFGH-…") and sends it to get the activation key for that computer.
/// One trait may change (a reinstall of Windows, a repaired board) and the computer is still recognized; two changes are another computer.
/// </summary>
public sealed class MachineCode
{
    private const byte Version = 1;
    private const int TraitBytes = 5;
    public const int TraitCount = 3;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32: no I, L, O, U

    private readonly byte[][] _traits;

    private MachineCode(byte[][] traits) => _traits = traits;

    /// <summary>Builds the code from the raw trait values; a missing or generic value counts as unknown.</summary>
    public static MachineCode FromTraits(params string?[] values)
    {
        if (values.Length != TraitCount) throw new ArgumentException($"Se esperaban {TraitCount} datos del equipo.", nameof(values));
        var traits = values.Select((v, i) => IsUseful(v)
            ? SHA256.HashData(Encoding.UTF8.GetBytes($"ABS-POS|{i}|{v!.Trim().ToUpperInvariant()}"))[..TraitBytes]
            : new byte[TraitBytes]).ToArray();
        if (traits.All(IsUnknown)) throw new InvalidOperationException("No se pudo identificar esta computadora.");
        return new MachineCode(traits);
    }

    /// <summary>Reads a code typed or pasted by a person: dashes, spaces and case do not matter; O/I/L are read as 0/1/1.</summary>
    public static bool TryParse(string? text, out MachineCode code)
    {
        code = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.ToUpperInvariant().Replace("-", "").Replace(" ", "").Replace('O', '0').Replace('I', '1').Replace('L', '1');
        var bytes = FromBase32(clean);
        if (bytes is null || bytes.Length != 2 + TraitCount * TraitBytes || bytes[0] != Version) return false;
        if (SHA256.HashData(bytes.AsSpan(0, bytes.Length - 1))[0] != bytes[^1]) return false; // typo
        var traits = Enumerable.Range(0, TraitCount).Select(i => bytes.AsSpan(1 + i * TraitBytes, TraitBytes).ToArray()).ToArray();
        if (traits.All(IsUnknown)) return false;
        code = new MachineCode(traits);
        return true;
    }

    /// <summary>
    /// Whether this computer is the one a license was issued for: at least two known traits must match
    /// (or the only one known when the licensed computer exposed just one).
    /// </summary>
    public bool Matches(MachineCode licensed)
    {
        var known = licensed._traits.Count(t => !IsUnknown(t));
        var same = Enumerable.Range(0, TraitCount).Count(i => !IsUnknown(licensed._traits[i]) && licensed._traits[i].AsSpan().SequenceEqual(_traits[i]));
        return same >= Math.Min(2, known);
    }

    public override string ToString()
    {
        var bytes = new byte[2 + TraitCount * TraitBytes];
        bytes[0] = Version;
        for (var i = 0; i < TraitCount; i++) _traits[i].CopyTo(bytes, 1 + i * TraitBytes);
        bytes[^1] = SHA256.HashData(bytes.AsSpan(0, bytes.Length - 1))[0];
        var text = ToBase32(bytes);
        return string.Join("-", Enumerable.Range(0, (text.Length + 3) / 4).Select(i => text.Substring(i * 4, Math.Min(4, text.Length - i * 4))));
    }

    private static bool IsUnknown(byte[] trait) => trait.All(b => b == 0);

    /// <summary>Firmware often reports placeholders instead of a real id; those identify nothing.</summary>
    public static bool IsUseful(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().ToUpperInvariant();
        string[] placeholders = ["TO BE FILLED BY O.E.M.", "DEFAULT STRING", "NONE", "N/A", "NOT APPLICABLE", "SYSTEM SERIAL NUMBER",
            "NOT SPECIFIED", "BASE BOARD SERIAL NUMBER", "0", "123456789", "INVALID"];
        if (placeholders.Contains(v)) return false;
        var chars = v.Where(char.IsLetterOrDigit).Distinct().ToList();
        return chars.Count > 1; // "FFFFFFFF-FFFF-…", "00000000-…"
    }

    private static string ToBase32(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[]? FromBase32(string text)
    {
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0) return null;
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
            buffer &= (1 << bits) - 1;
        }
        return bytes.ToArray();
    }
}
