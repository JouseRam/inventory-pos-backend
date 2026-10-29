using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;

namespace InventoryPOS.Domain.Entities;

/// <summary>Cash register shift: opening float, sales, cash in/out and closing count.</summary>
public class CashSession : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = CashSessionStatus.Open;
    public decimal OpeningAmount { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }
    public string? Notes { get; set; }

    public ICollection<CashMovement> Movements { get; set; } = [];
    public ICollection<Sale> Sales { get; set; } = [];
}

public class CashMovement : BaseEntity
{
    public int CashSessionId { get; set; }
    public string Type { get; set; } = CashMovementTypes.Out;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? UserId { get; set; }

    public CashSession CashSession { get; set; } = null!;
}
