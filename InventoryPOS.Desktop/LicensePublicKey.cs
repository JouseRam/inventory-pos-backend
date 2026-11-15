namespace InventoryPOS.Desktop;

/// <summary>
/// The vendor's public key for activation keys. Create the pair once with the license tool
/// (dotnet run --project InventoryPOS.LicenseTool -- llaves) and paste the public key here before publishing.
/// The private key never goes in the repository or the program.
/// </summary>
internal static class LicensePublicKey
{
    public const string Value = "";
}
