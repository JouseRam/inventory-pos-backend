using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;

namespace InventoryPOS.Domain.Entities;

public class Sale : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int CashSessionId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    /// <summary>Tax included in Total (prices are tax inclusive).</summary>
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }
    public string Status { get; set; } = SaleStatus.Completed;
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? UserId { get; set; }

    public Customer? Customer { get; set; }
    public CashSession CashSession { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = [];
    public ICollection<SalePayment> Payments { get; set; } = [];
}

public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }
    public int VariantId { get; set; }
    /// <summary>Snapshot of the variant name at sale time.</summary>
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Subtotal { get; set; }

    public Sale Sale { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}

public class SalePayment : BaseEntity
{
    public int SaleId { get; set; }
    public string Method { get; set; } = PaymentMethods.Cash;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }

    public Sale Sale { get; set; } = null!;
}
