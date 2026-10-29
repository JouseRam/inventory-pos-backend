using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>Goods receipt from a supplier.</summary>
public class Purchase : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? UserId { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = [];
}

public class PurchaseItem : BaseEntity
{
    public int PurchaseId { get; set; }
    public int VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int? BatchId { get; set; }

    public Purchase Purchase { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public ProductBatch? Batch { get; set; }
}
