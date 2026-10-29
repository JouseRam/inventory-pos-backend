using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Printing;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/sales")]
[RequireModule(PosModules.Sales)]
public class SalesController(SaleService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SaleDto>> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? paymentMethod,
        [FromQuery] int? customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        service.ListAsync(from, to, paymentMethod, customerId, page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<SaleDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(SaleCreateDto dto, CancellationToken ct)
    {
        var sale = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = sale.Id }, sale);
    }

    [HttpPost("{id:int}/cancel")]
    public Task<SaleDto> Cancel(int id, SaleCancelDto dto, CancellationToken ct) => service.CancelAsync(id, dto, ct);
}

[ApiController]
[Route("api/cash-sessions")]
[RequireModule(PosModules.CashRegister)]
public class CashSessionsController(CashSessionService service) : ControllerBase
{
    /// <summary>The caller's open session, or 204 when the register is closed.</summary>
    [HttpGet("current")]
    [AnyUser]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var session = await service.GetCurrentAsync(ct);
        return session == null ? NoContent() : Ok(session);
    }

    [HttpGet]
    public Task<PagedResult<CashSessionListItemDto>> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        service.ListAsync(from, to, page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<CashSummaryDto> Get(int id, CancellationToken ct) => service.GetSummaryAsync(id, ct);

    [HttpPost("open")]
    public Task<CashSummaryDto> Open(CashOpenDto dto, CancellationToken ct) => service.OpenAsync(dto, ct);

    [HttpPost("{id:int}/movements")]
    public Task<CashSummaryDto> AddMovement(int id, CashMovementSaveDto dto, CancellationToken ct) => service.AddMovementAsync(id, dto, ct);

    [HttpPost("{id:int}/close")]
    public Task<CashSummaryDto> Close(int id, CashCloseDto dto, CancellationToken ct) => service.CloseAsync(id, dto, ct);
}

[ApiController]
[Route("api/printing")]
[RequireModule(PosModules.Settings)]
public class PrintingController(PrintingService service) : ControllerBase
{
    /// <summary>Whether a receipt printer is configured and should print after each sale.</summary>
    [HttpGet("status")]
    [AnyUser]
    public Task<PrinterStatusDto> Status(CancellationToken ct) => service.GetStatusAsync(ct);

    [HttpGet("printers")]
    public IReadOnlyList<string> Printers() => service.ListPrinters();

    [HttpPost("sales/{id:int}")]
    [RequireModule(PosModules.Sales)]
    public async Task<IActionResult> PrintSale(int id, [FromQuery] bool reprint, CancellationToken ct)
    {
        await service.PrintSaleAsync(id, reprint, ct);
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        await service.PrintTestAsync(ct);
        return NoContent();
    }

    [HttpPost("drawer")]
    [RequireModule(PosModules.CashRegister)]
    public async Task<IActionResult> OpenDrawer(CancellationToken ct)
    {
        await service.OpenDrawerAsync(ct);
        return NoContent();
    }
}
