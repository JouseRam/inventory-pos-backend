using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>A CFDI stamped for a sale through the invoicing provider.</summary>
public class Invoice : BaseEntity
{
    public int SaleId { get; set; }
    /// <summary>Provider that stamped it (e.g. Facturama) and its own id for downloads and cancellation.</summary>
    public string Provider { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    /// <summary>Folio fiscal (UUID) assigned by the SAT.</summary>
    public string Uuid { get; set; } = string.Empty;
    public string Status { get; set; } = Constants.InvoiceStatus.Active;
    public string ReceiverRfc { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string CfdiUse { get; set; } = string.Empty;
    /// <summary>c_FormaPago code, e.g. 01 cash.</summary>
    public string PaymentForm { get; set; } = string.Empty;
    public decimal Total { get; set; }
    /// <summary>Stamped in the provider's test environment: not valid before the SAT.</summary>
    public bool Sandbox { get; set; }
    /// <summary>Stamped XML; the PDF is kept too so the invoice can be reprinted without internet.</summary>
    public string? Xml { get; set; }
    public byte[]? Pdf { get; set; }
    public string? UserId { get; set; }
    public string? CancelMotive { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Sale Sale { get; set; } = null!;
}

/// <summary>
/// This installation's account with the invoicing provider. Never synced: the password is encrypted
/// with this installation's key, and each computer that invoices is configured on its own.
/// </summary>
public class InvoicingAccount : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    /// <summary>True for the provider's test environment, where stamps are free and not valid before the SAT.</summary>
    public bool Sandbox { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string? ProtectedPassword { get; set; }
    /// <summary>Default clave de producto o servicio (c_ClaveProdServ) for products without their own.</summary>
    public string DefaultProductCode { get; set; } = "01010101";
    /// <summary>Series printed on the invoices, e.g. F.</summary>
    public string? Serie { get; set; }
    // ── CSD uploaded to the provider (the files themselves stay with the provider) ──
    public string? CsdRfc { get; set; }
    public string? CsdCertificateNumber { get; set; }
    public DateTime? CsdValidUntil { get; set; }
    public DateTime? CsdUploadedAt { get; set; }
}
