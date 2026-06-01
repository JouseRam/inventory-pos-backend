namespace InventoryPOS.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; } = 5;
    public string Unit { get; set; } = "pza";
    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<SaleItem> SaleItems { get; set; } = [];
}
