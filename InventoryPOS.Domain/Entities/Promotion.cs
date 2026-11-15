using InventoryPOS.Domain.Common;

namespace InventoryPOS.Domain.Entities;

/// <summary>A promotion to send to customers: an image, a slogan and a message.</summary>
public class Promotion : BaseEntity
{
    /// <summary>Slogan or headline, e.g. "2x1 en tintes este fin de semana".</summary>
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>The picture, already reduced by the page (it is sent as a phone image, not printed).</summary>
    public byte[]? Image { get; set; }
    public string? ImageType { get; set; }
    public string? UserId { get; set; }

    public ICollection<PromotionRecipient> Recipients { get; set; } = [];
}

/// <summary>One customer the promotion goes to, and whether it was sent.</summary>
public class PromotionRecipient : BaseEntity
{
    public int PromotionId { get; set; }
    public int CustomerId { get; set; }
    /// <summary>Name and phone as they were when the customer was added, so the list reads the same later.</summary>
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = Constants.PromotionRecipientStatus.Pending;
    public DateTime? SentAt { get; set; }

    public Promotion Promotion { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
