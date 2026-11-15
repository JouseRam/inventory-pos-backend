namespace InventoryPOS.Domain.Common;

/// <summary>Mexican phone numbers: stored as 10 digits so "771 104 2290", "(771) 104-2290" and "+52 7711042290" are the same customer.</summary>
public static class PhoneNumber
{
    public const string CountryCode = "52";

    /// <summary>The 10 national digits, or null when the text is not a Mexican number.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith(CountryCode)) digits = digits[2..];
        if (digits.Length == 13 && digits.StartsWith(CountryCode + "1")) digits = digits[3..]; // old mobile prefix 52 1
        return digits.Length == 10 ? digits : null;
    }

    /// <summary>"771 104 2290".</summary>
    public static string Format(string digits) =>
        digits.Length == 10 ? $"{digits[..3]} {digits[3..6]} {digits[6..]}" : digits;
}
