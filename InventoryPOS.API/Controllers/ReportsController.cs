using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var today = DateTime.Today;
        var weekAgo = today.AddDays(-6);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var todayEnd = today.AddDays(1);

        var salesToday = await db.Sales
            .Where(s => s.CreatedAt >= today && s.CreatedAt < todayEnd && s.Status != "Cancelada")
            .GroupBy(s => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(s => s.Total) })
            .FirstOrDefaultAsync();

        var salesWeek = await db.Sales
            .Where(s => s.CreatedAt >= weekAgo && s.Status != "Cancelada")
            .SumAsync(s => (decimal?)s.Total) ?? 0;

        var salesMonth = await db.Sales
            .Where(s => s.CreatedAt >= monthStart && s.Status != "Cancelada")
            .SumAsync(s => (decimal?)s.Total) ?? 0;

        var productCount = await db.Products.CountAsync(p => p.IsActive);
        var lowStockCount = await db.Products.CountAsync(p => p.IsActive && p.Stock <= p.MinStock);
        var customerCount = await db.Customers.CountAsync(c => c.IsActive);

        var recentSales = await db.Sales
            .Include(s => s.Customer)
            .Where(s => s.Status != "Cancelada")
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new {
                s.Id, s.Folio, s.Total, s.PaymentMethod, s.Status, s.CreatedAt,
                CustomerName = s.Customer != null ? s.Customer.Name : "Mostrador",
                ItemCount = s.Items.Count
            }).ToListAsync();

        var lowStockProducts = await db.Products.Include(p => p.Category)
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock).Take(5)
            .Select(p => new {
                p.Id, p.Name, p.Stock, p.MinStock, p.Unit,
                Category = p.Category != null ? p.Category.Name : null
            }).ToListAsync();

        return Ok(new {
            SalesToday = salesToday?.Total ?? 0,
            CountToday = salesToday?.Count ?? 0,
            SalesWeek = salesWeek,
            SalesMonth = salesMonth,
            ProductCount = productCount,
            LowStockCount = lowStockCount,
            CustomerCount = customerCount,
            RecentSales = recentSales,
            LowStockProducts = lowStockProducts
        });
    }

    [HttpGet("sales-by-day")]
    public async Task<IActionResult> SalesByDay([FromQuery] int days = 30)
    {
        var from = DateTime.Today.AddDays(-(days - 1));
        var data = await db.Sales
            .Where(s => s.CreatedAt >= from && s.Status != "Cancelada")
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new {
                Date = g.Key,
                Total = g.Sum(s => s.Total),
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] int days = 30, [FromQuery] int limit = 10)
    {
        var from = DateTime.Today.AddDays(-days);
        var data = await db.SaleItems
            .Where(i => i.Sale.CreatedAt >= from && i.Sale.Status != "Cancelada")
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new {
                g.Key.ProductId,
                g.Key.ProductName,
                TotalQty = g.Sum(i => i.Quantity),
                TotalRevenue = g.Sum(i => i.Subtotal)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(limit)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("by-category")]
    public async Task<IActionResult> ByCategory([FromQuery] int days = 30)
    {
        var from = DateTime.Today.AddDays(-days);
        var data = await db.SaleItems
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Where(i => i.Sale.CreatedAt >= from && i.Sale.Status != "Cancelada")
            .GroupBy(i => i.Product.Category != null ? i.Product.Category.Name : "Sin categoría")
            .Select(g => new {
                Category = g.Key,
                TotalRevenue = g.Sum(i => i.Subtotal),
                TotalQty = g.Sum(i => i.Quantity)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> PaymentMethods([FromQuery] int days = 30)
    {
        var from = DateTime.Today.AddDays(-days);
        var data = await db.Sales
            .Where(s => s.CreatedAt >= from && s.Status != "Cancelada")
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new {
                Method = g.Key,
                Total = g.Sum(s => s.Total),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from ?? DateTime.Today.AddDays(-30);
        var end = (to ?? DateTime.Today).AddDays(1);

        var sales = await db.Sales
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end && s.Status != "Cancelada")
            .ToListAsync();

        return Ok(new {
            From = start.Date,
            To = end.AddDays(-1).Date,
            TotalSales = sales.Count,
            TotalRevenue = sales.Sum(s => s.Total),
            TotalDiscount = sales.Sum(s => s.Discount),
            TotalTax = sales.Sum(s => s.Tax),
            AverageTicket = sales.Count > 0 ? sales.Average(s => s.Total) : 0
        });
    }
}
