namespace InventoryPOS.Infrastructure.Identity;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "InventoryPOS";
    public string Audience { get; set; } = "InventoryPOS";
    /// <summary>HMAC key, at least 32 characters. Keep it in user-secrets or environment variables, never in the repo.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 10;
}
