using InventoryPOS.Domain;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? q)
    {
        var query = db.Customers.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || (c.Phone != null && c.Phone.Contains(q)));

        var customers = await query.OrderBy(c => c.Name)
            .Select(c => new {
                c.Id, c.Name, c.Phone, c.Email, c.Address, c.IsActive, c.CreatedAt,
                SalesCount = c.Sales.Count
            }).ToListAsync();
        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await db.Customers.FindAsync(id);
        return c == null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
    {
        var c = new Customer {
            Name = dto.Name, Phone = dto.Phone,
            Email = dto.Email, Address = dto.Address,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto)
    {
        var c = await db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        c.Name = dto.Name; c.Phone = dto.Phone;
        c.Email = dto.Email; c.Address = dto.Address;
        await db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        c.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CustomerDto(string Name, string? Phone, string? Email, string? Address);
