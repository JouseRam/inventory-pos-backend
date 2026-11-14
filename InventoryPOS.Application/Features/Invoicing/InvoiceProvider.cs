namespace InventoryPOS.Application.Features.Invoicing;

/// <summary>Credentials of the invoicing account, already decrypted, for one call to the provider.</summary>
public record ProviderAccount(bool Sandbox, string UserName, string Password);

public record CsdFiles(string Rfc, byte[] Certificate, byte[] PrivateKey, string Password);

public record CfdiParty(string Rfc, string Name, string TaxRegime, string ZipCode);

public record CfdiConcept(
    string ProductCode, string UnitCode, string UnitName, string? Sku, string Description,
    decimal Quantity, decimal UnitValue, decimal Amount, decimal Discount, decimal TaxBase, decimal TaxRate, decimal Tax, decimal Total);

/// <summary>A provider-neutral CFDI 4.0 de ingreso, paid in one exhibition (PUE).</summary>
public record CfdiRequest(
    CfdiParty Issuer, CfdiParty Receiver, string CfdiUse, string? Serie, string Folio, string PaymentForm,
    IReadOnlyList<CfdiConcept> Concepts);

public record StampedCfdi(string ProviderId, string Uuid, decimal Total);

public enum CfdiFileFormat { Xml, Pdf }

/// <summary>c_MotivoCancelacion. 01 needs the UUID of the invoice that replaces the cancelled one.</summary>
public static class CancelMotives
{
    public const string WithErrorsReplaced = "01";
    public const string WithErrorsNoReplacement = "02";
    public const string OperationNotCarriedOut = "03";
    public const string GlobalInvoiceOperation = "04";

    public static readonly IReadOnlyList<SatCatalogEntry> All =
    [
        new(WithErrorsReplaced, "Comprobante emitido con errores con relación (se sustituye por otra factura)", TaxpayerKind.Both),
        new(WithErrorsNoReplacement, "Comprobante emitido con errores sin relación", TaxpayerKind.Both),
        new(OperationNotCarriedOut, "No se llevó a cabo la operación", TaxpayerKind.Both),
        new(GlobalInvoiceOperation, "Operación nominativa relacionada en una factura global", TaxpayerKind.Both),
    ];
}

public record CancelOutcome(bool Cancelled, string Status, string? Message);

/// <summary>
/// A PAC that stamps CFDI. Adding another provider means implementing this interface and registering it;
/// the rest of the module only speaks these types.
/// </summary>
public interface IInvoiceProvider
{
    /// <summary>Name stored on the account and on each invoice, e.g. "Facturama".</summary>
    string Name { get; }

    /// <summary>Checks the credentials; throws <see cref="InvoicingException"/> when they are rejected.</summary>
    Task VerifyAsync(ProviderAccount account, CancellationToken ct);

    /// <summary>Hands the issuer's CSD to the provider, which keeps it and seals with it.</summary>
    Task UploadCsdAsync(ProviderAccount account, CsdFiles csd, CancellationToken ct);

    Task<StampedCfdi> StampAsync(ProviderAccount account, CfdiRequest request, CancellationToken ct);

    Task<byte[]> DownloadAsync(ProviderAccount account, string providerId, CfdiFileFormat format, CancellationToken ct);

    Task<CancelOutcome> CancelAsync(ProviderAccount account, string providerId, string motive, string? replacementUuid, CancellationToken ct);
}

/// <summary>The provider rejected the request; the message is meant for the user.</summary>
public class InvoicingException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The provider could not be reached: invoicing needs an internet connection.</summary>
public class InvoicingOfflineException(Exception? inner = null)
    : InvoicingException("No hay conexión a internet. Para facturar necesitas estar conectado; la venta queda registrada y puedes facturarla después.", inner);

/// <summary>Encrypts secrets kept in the database with a key that only this installation has.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    /// <summary>Null when the value was encrypted with another key (e.g. the installation key changed).</summary>
    string? Unprotect(string protectedValue);
}
