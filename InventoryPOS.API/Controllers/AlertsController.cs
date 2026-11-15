using InventoryPOS.Application.Features.Alerts;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

/// <summary>Notifications for the administrator: presentations at or under their minimum stock.</summary>
[ApiController]
[Route("api/alerts")]
public class AlertsController(StockAlertService service) : ControllerBase
{
    [HttpGet("stock")]
    public Task<StockAlertsDto> Stock(CancellationToken ct) => service.GetAsync(ct);

    [HttpPost("stock/read")]
    public async Task<IActionResult> MarkRead(CancellationToken ct)
    {
        await service.MarkAllReadAsync(ct);
        return NoContent();
    }
}
