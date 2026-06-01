using InventoryPOS.Domain;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? paymentMethod,
        [FromQuery] int? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = db.Sales.Include(s => s.Customer).AsQueryable();

        if (from.HasValue) query = query.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.CreatedAt <= to.Value.AddDays(1));
        if (!string.IsNullOrWhiteSpace(paymentMethod)) query = query.Where(s => s.PaymentMethod == paymentMethod);
        if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId);

        var total = await query.CountAsync();
        var sales = await query.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new {
                s.Id, s.Folio, s.Subtotal, s.Discount, s.Tax, s.Total,
                s.PaymentMethod, s.AmountPaid, s.Change, s.Status, s.Notes, s.CreatedAt,
                CustomerName = s.Customer != null ? s.Customer.Name : "Mostrador",
                ItemCount = s.Items.Count
            }).ToListAsync();

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = sales });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sale = await db.Sales.Include(s => s.Customer).Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();

        return Ok(new {
            sale.Id, sale.Folio, sale.Subtotal, sale.Discount, sale.Tax, sale.Total,
            sale.PaymentMethod, sale.AmountPaid, sale.Change, sale.Status, sale.Notes, sale.CreatedAt,
            CustomerName = sale.Customer?.Name ?? "Mostrador",
            sale.CustomerId,
            Items = sale.Items.Select(i => new {
                i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Discount, i.Subtotal
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaleCreateDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { error = "La venta debe tener al menos un producto." });

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id) && p.IsActive).ToListAsync();

        foreach (var item in dto.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null)
                return BadRequest(new { error = $"Producto ID {item.ProductId} no encontrado o inactivo." });
            if (product.Stock < item.Quantity)
                return BadRequest(new { error = $"Stock insuficiente para '{product.Name}'. Disponible: {product.Stock} {product.Unit}" });
        }

        var saleItems = dto.Items.Select(i =>
        {
            var p = products.First(x => x.Id == i.ProductId);
            return new SaleItem {
                ProductId = i.ProductId,
                ProductName = p.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice > 0 ? i.UnitPrice : p.Price,
                Discount = i.Discount,
                Subtotal = Math.Round(i.Quantity * (i.UnitPrice > 0 ? i.UnitPrice : p.Price) - i.Discount, 2)
            };
        }).ToList();

        var subtotal = saleItems.Sum(i => i.Subtotal);
        var total = Math.Round(subtotal - dto.Discount + dto.Tax, 2);
        if (total < 0) total = 0;

        var count = await db.Sales.CountAsync();
        var folio = $"V-{(count + 1):D4}";

        var sale = new Sale {
            Folio = folio,
            CustomerId = dto.CustomerId,
            Subtotal = subtotal,
            Discount = dto.Discount,
            Tax = dto.Tax,
            Total = total,
            PaymentMethod = dto.PaymentMethod,
            AmountPaid = dto.AmountPaid,
            Change = Math.Max(0, Math.Round(dto.AmountPaid - total, 2)),
            Notes = dto.Notes,
            Items = saleItems,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in dto.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            product.Stock -= item.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
        }

        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, new {
            sale.Id, sale.Folio, sale.Total, sale.Change, sale.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var sale = await db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null) return NotFound();
        if (sale.Status == "Cancelada") return BadRequest(new { error = "La venta ya está cancelada." });

        // Restore stock
        var productIds = sale.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var item in sale.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                product.Stock += item.Quantity;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        sale.Status = "Cancelada";
        await db.SaveChangesAsync();
        return Ok(new { sale.Id, sale.Folio, sale.Status });
    }
}

public record SaleItemDto(int ProductId, decimal Quantity, decimal UnitPrice, decimal Discount);
public record SaleCreateDto(
    int? CustomerId,
    string PaymentMethod,
    decimal AmountPaid,
    decimal Discount,
    decimal Tax,
    string? Notes,
    List<SaleItemDto> Items);
