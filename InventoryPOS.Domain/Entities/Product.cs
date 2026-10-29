using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;

namespace InventoryPOS.Domain.Entities;

/// <summary>Parent product (e.g. a dye line); what is sold are its variants.</summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? BrandId { get; set; }
    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
    /// <summary>When true, stock is tracked by lot with expiration date and sold FEFO.</summary>
    public bool TracksBatches { get; set; }
    public bool IsActive { get; set; } = true;

    public Brand? Brand { get; set; }
    public Category? Category { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = [];
}

/// <summary>Sellable unit: one shade and size of a product.</summary>
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    /// <summary>Shade code as printed on the box, e.g. 7.1.</summary>
    public string? ShadeCode { get; set; }
    public string? ShadeName { get; set; }
    public decimal? Size { get; set; }
    public string Unit { get; set; } = UnitsOfMeasure.Piece;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<ProductBatch> Batches { get; set; } = [];

    /// <summary>Name printed on tickets: product, shade and size.</summary>
    public string DisplayName()
    {
        var parts = new List<string> { Product?.Name ?? string.Empty };
        var shade = $"{ShadeCode} {ShadeName}".Trim();
        if (shade.Length > 0) parts.Add(shade);
        if (Size.HasValue) parts.Add($"{Size.Value:0.##} {Unit}");
        return string.Join(" - ", parts.Where(p => p.Length > 0));
    }
}

public class ProductBatch : BaseEntity
{
    public int VariantId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
    public decimal Quantity { get; set; }

    public ProductVariant Variant { get; set; } = null!;
}

/// <summary>Kardex row; every stock change is recorded here.</summary>
public class InventoryMovement : BaseEntity
{
    public int VariantId { get; set; }
    public int? BatchId { get; set; }
    public string Type { get; set; } = string.Empty;
    /// <summary>Signed quantity: positive adds stock, negative removes it.</summary>
    public decimal Quantity { get; set; }
    public decimal StockAfter { get; set; }
    public decimal UnitCost { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? UserId { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public ProductBatch? Batch { get; set; }
}
