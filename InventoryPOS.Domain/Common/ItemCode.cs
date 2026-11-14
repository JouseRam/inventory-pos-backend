using System.Security.Cryptography;

namespace InventoryPOS.Domain.Common;

/// <summary>
/// The system's own ID for what goes on a label: P + 8 characters for a presentation, L + 8 for a lot.
/// Only digits and uppercase letters without I, L, O or U (Crockford base 32): a barcode scanner types it like a keyboard,
/// and symbols such as "-" come out differently when Windows is set to a Spanish layout; the missing letters avoid
/// confusing 1/I/L and 0/O when someone types it by hand. 40 random bits keep IDs created on different registers apart.
/// </summary>
public static class ItemCode
{
    public const char VariantPrefix = 'P';
    public const char BatchPrefix = 'L';
    public const int Length = 9;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string New(char prefix)
    {
        Span<byte> random = stackalloc byte[Length - 1];
        RandomNumberGenerator.Fill(random);
        Span<char> code = stackalloc char[Length];
        code[0] = prefix;
        for (var i = 1; i < Length; i++) code[i] = Alphabet[random[i - 1] & 31];
        return new string(code);
    }

    /// <summary>
    /// The ID as typed or scanned: uppercase, without spaces or dashes, and with the letters a person confuses read as
    /// the digits they look like. Null when the text is not shaped like an ID.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var text = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        if (text.Length != Length || (text[0] != VariantPrefix && text[0] != BatchPrefix)) return null;
        var body = text[1..].Replace('O', '0').Replace('I', '1').Replace('L', '1');
        return body.All(c => Alphabet.Contains(c)) ? text[0] + body : null;
    }
}
