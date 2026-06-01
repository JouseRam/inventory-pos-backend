using InventoryPOS.Domain;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? q)
    {
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Name.Contains(q) || (s.ContactName != null && s.ContactName.Contains(q)));

        var suppliers = await query.OrderBy(s => s.Name)
            .Select(s => new {
                s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.IsActive,
                ProductCount = s.Products.Count(p => p.IsActive)
            }).ToListAsync();
        return Ok(suppliers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await db.Suppliers.FindAsync(id);
        return s == null ? NotFound() : Ok(s);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupplierDto dto)
    {
        var s = new Supplier {
            Name = dto.Name, ContactName = dto.ContactName,
            Phone = dto.Phone, Email = dto.Email, Address = dto.Address
        };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync();
        return Ok(s);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SupplierDto dto)
    {
        var s = await db.Suppliers.FindAsync(id);
        if (s == null) return NotFound();
        s.Name = dto.Name; s.ContactName = dto.ContactName;
        s.Phone = dto.Phone; s.Email = dto.Email; s.Address = dto.Address;
        await db.SaveChangesAsync();
        return Ok(s);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Suppliers.FindAsync(id);
        if (s == null) return NotFound();
        s.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record SupplierDto(string Name, string? ContactName, string? Phone, string? Email, string? Address);
