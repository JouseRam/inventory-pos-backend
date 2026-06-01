using InventoryPOS.Domain;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cats = await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new {
                c.Id, c.Name, c.Description, c.IsActive,
                ProductCount = c.Products.Count(p => p.IsActive)
            }).ToListAsync();
        return Ok(cats);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cat = await db.Categories.FindAsync(id);
        return cat == null ? NotFound() : Ok(cat);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryDto dto)
    {
        var cat = new Category { Name = dto.Name, Description = dto.Description };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryDto dto)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        cat.Name = dto.Name;
        cat.Description = dto.Description;
        await db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id && p.IsActive);
        if (hasProducts) return BadRequest("No se puede eliminar una categoría con productos activos.");
        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CategoryDto(string Name, string? Description);
