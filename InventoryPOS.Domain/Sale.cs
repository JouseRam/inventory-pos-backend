namespace InventoryPOS.Domain;

public class Sale
{
    public int Id { get; set; }
    public string Folio { get; set; } = "";
    public int? CustomerId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "Efectivo";
    public decimal AmountPaid { get; set; }
    public decimal Change { get; set; }
    public string Status { get; set; } = "Completada";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }
    public ICollection<SaleItem> Items { get; set; } = [];
}
