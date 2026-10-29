using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/inventory")]
[RequireModule(PosModules.Inventory)]
public class InventoryController(InventoryService service) : ControllerBase
{
    /// <summary>Kardex; dates are local calendar days.</summary>
    [HttpGet("movements")]
    public Task<PagedResult<MovementDto>> Movements([FromQuery] int? variantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        service.GetMovementsAsync(variantId, from, to, page, pageSize, ct);

    [HttpGet("batches/{variantId:int}")]
    public Task<IReadOnlyList<BatchDto>> Batches(int variantId, CancellationToken ct) => service.GetBatchesAsync(variantId, ct);

    [HttpGet("expiring")]
    public Task<IReadOnlyList<BatchDto>> Expiring([FromQuery] int days = 60, CancellationToken ct = default) => service.GetExpiringAsync(days, ct);

    [HttpPost("adjustments")]
    public Task<MovementDto> Adjust(AdjustmentDto dto, CancellationToken ct) => service.AdjustAsync(dto, ct);
}

[ApiController]
[Route("api/purchases")]
[RequireModule(PosModules.Purchases)]
public class PurchasesController(PurchaseService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseDto>> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? supplierId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        service.ListAsync(from, to, supplierId, page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<PurchaseDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Receive(PurchaseCreateDto dto, CancellationToken ct)
    {
        var purchase = await service.ReceiveAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = purchase.Id }, purchase);
    }
}
