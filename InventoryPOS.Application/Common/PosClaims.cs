namespace InventoryPOS.Application.Common;

/// <summary>Claim names written into the JWT and read back by the API's current-user accessor.</summary>
public static class PosClaims
{
    public const string UserId = "sub";
    public const string UserName = "unique_name";
    public const string FullName = "name";
    public const string SuperAdmin = "super_admin";
}
