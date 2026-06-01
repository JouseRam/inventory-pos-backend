using InventoryPOS.Domain;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] bool? active,
        [FromQuery] bool? lowStock)
    {
        var query = db.Products.Include(p => p.Category).Include(p => p.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || (p.SKU != null && p.SKU.Contains(q)) || (p.Barcode != null && p.Barcode.Contains(q)));
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);
        if (active.HasValue)
            query = query.Where(p => p.IsActive == active.Value);
        else
            query = query.Where(p => p.IsActive);
        if (lowStock == true)
            query = query.Where(p => p.Stock <= p.MinStock);

        var products = await query.OrderBy(p => p.Name).Select(p => new {
            p.Id, p.Name, p.Description, p.SKU, p.Barcode,
            p.Price, p.Cost, p.Stock, p.MinStock, p.Unit,
            p.CategoryId, CategoryName = p.Category != null ? p.Category.Name : null,
            p.SupplierId, SupplierName = p.Supplier != null ? p.Supplier.Name : null,
            p.IsActive, p.CreatedAt, p.UpdatedAt,
            LowStock = p.Stock <= p.MinStock
        }).ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await db.Products.Include(p => p.Category).Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();
        return Ok(new {
            p.Id, p.Name, p.Description, p.SKU, p.Barcode,
            p.Price, p.Cost, p.Stock, p.MinStock, p.Unit,
            p.CategoryId, CategoryName = p.Category?.Name,
            p.SupplierId, SupplierName = p.Supplier?.Name,
            p.IsActive, p.CreatedAt, p.UpdatedAt
        });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
        var products = await db.Products
            .Where(p => p.IsActive && (p.Name.Contains(q) || (p.SKU != null && p.SKU.Contains(q)) || (p.Barcode != null && p.Barcode.Contains(q))))
            .OrderBy(p => p.Name).Take(20)
            .Select(p => new { p.Id, p.Name, p.SKU, p.Barcode, p.Price, p.Stock, p.Unit, p.CategoryId })
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock()
    {
        var products = await db.Products.Include(p => p.Category)
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .Select(p => new {
                p.Id, p.Name, p.Stock, p.MinStock, p.Unit,
                CategoryName = p.Category != null ? p.Category.Name : null
            }).ToListAsync();
        return Ok(products);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductDto dto)
    {
        var product = new Product {
            Name = dto.Name, Description = dto.Description,
            SKU = dto.SKU, Barcode = dto.Barcode,
            Price = dto.Price, Cost = dto.Cost,
            Stock = dto.Stock, MinStock = dto.MinStock,
            Unit = dto.Unit ?? "pza",
            CategoryId = dto.CategoryId, SupplierId = dto.SupplierId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Ok(new { product.Id, product.Name, product.Price, product.Stock });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductDto dto)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();
        product.Name = dto.Name; product.Description = dto.Description;
        product.SKU = dto.SKU; product.Barcode = dto.Barcode;
        product.Price = dto.Price; product.Cost = dto.Cost;
        product.Stock = dto.Stock; product.MinStock = dto.MinStock;
        product.Unit = dto.Unit ?? product.Unit;
        product.CategoryId = dto.CategoryId; product.SupplierId = dto.SupplierId;
        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product == null) return NotFound();
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ProductDto(
    string Name, string? Description, string? SKU, string? Barcode,
    decimal Price, decimal Cost, decimal Stock, decimal MinStock,
    string? Unit = "pza", int? CategoryId = null, int? SupplierId = null, bool IsActive = true);
