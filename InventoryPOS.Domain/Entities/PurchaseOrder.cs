using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>An order to a supplier: drafted (suggested or by hand), sent, shipped with a tracking number, received.</summary>
public class PurchaseOrder : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string Status { get; set; } = Constants.PurchaseOrderStatus.Draft;
    public string? Notes { get; set; }
    public DateTime? SentAt { get; set; }
    /// <summary>Carrier code (see the carriers list) and the guide number the supplier gives once it ships.</summary>
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    /// <summary>The reception that brought the goods in.</summary>
    public int? PurchaseId { get; set; }
    public string? UserId { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
    public ICollection<PurchaseOrderEvent> Events { get; set; } = [];
}

public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public int VariantId { get; set; }
    /// <summary>Snapshot of the presentation's name, as it went in the order.</summary>
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    /// <summary>Expected cost per piece (the last one paid), to estimate the order's amount.</summary>
    public decimal UnitCost { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

/// <summary>One line of the order's timeline: what the system recorded (sent, shipped, received) or what the user wrote.</summary>
public class PurchaseOrderEvent : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>True when the user wrote it (for example a movement read on the carrier's site).</summary>
    public bool Manual { get; set; }
    public string? UserId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
