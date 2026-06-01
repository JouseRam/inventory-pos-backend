namespace InventoryPOS.Domain;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = [];
}
