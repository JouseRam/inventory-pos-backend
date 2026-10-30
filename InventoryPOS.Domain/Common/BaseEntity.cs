namespace InventoryPOS.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }
    /// <summary>Global identity shared by the register and the central server; local Ids differ between them.</summary>
    public Guid Uid { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
