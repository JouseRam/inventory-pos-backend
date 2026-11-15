using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>
/// A presentation that reached its minimum stock. Open while it stays at or under the minimum; resolved when stock comes back.
/// The administrator sees it in the notifications bell until marking it as read.
/// </summary>
public class StockAlert : BaseEntity
{
    public int VariantId { get; set; }
    /// <summary>Stock and minimum as last seen, to describe the alert.</summary>
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ProductVariant Variant { get; set; } = null!;
}
