using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Orders;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

/// <summary>Orders to suppliers: suggestions, drafts, sending, tracking and reception. Same permission as receptions.</summary>
[ApiController]
[Route("api/orders")]
[RequireModule(PosModules.Purchases)]
public class PurchaseOrdersController(PurchaseOrderService service, SupplierStatsService stats) : ControllerBase
{
    /// <summary>How much was bought from each supplier in the last <paramref name="days"/> days and how often.</summary>
    [HttpGet("supplier-stats")]
    public Task<SupplierStatsDto> SupplierStats([FromQuery] int days = 365, CancellationToken ct = default) => stats.GetAsync(days, ct);

    /// <summary>One supplier: months, products with their cost change, and a message to ask for better terms.</summary>
    [HttpGet("supplier-stats/{supplierId:int}")]
    public Task<SupplierStatsDetailDto> SupplierStatsDetail(int supplierId, [FromQuery] int days = 365, CancellationToken ct = default) =>
        stats.GetSupplierAsync(supplierId, days, ct);

    /// <summary>What to reorder per supplier, from stock, minimums and the sales of the last <paramref name="days"/> days.</summary>
    [HttpGet("suggestions")]
    public Task<SuggestionsDto> Suggestions([FromQuery] int days = 30, [FromQuery] int coverDays = 30, CancellationToken ct = default) =>
        service.SuggestAsync(days, coverDays, ct);

    [HttpGet("carriers")]
    public IReadOnlyList<CarrierDto> CarriersList() => Carriers.All;

    [HttpGet]
    public Task<IReadOnlyList<OrderSummaryDto>> GetAll([FromQuery] string? status, CancellationToken ct) => service.ListAsync(status, ct);

    [HttpGet("{id:int}")]
    public Task<OrderDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public Task<OrderDto> Create(OrderSaveDto dto, CancellationToken ct) => service.CreateAsync(dto, ct);

    [HttpPut("{id:int}")]
    public Task<OrderDto> Update(int id, OrderSaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpPost("{id:int}/sent")]
    public Task<OrderDto> Sent(int id, CancellationToken ct) => service.MarkSentAsync(id, ct);

    [HttpPut("{id:int}/tracking")]
    public Task<OrderDto> Tracking(int id, TrackingSaveDto dto, CancellationToken ct) => service.SetTrackingAsync(id, dto, ct);

    [HttpPost("{id:int}/events")]
    public Task<OrderDto> AddEvent(int id, OrderEventSaveDto dto, CancellationToken ct) => service.AddEventAsync(id, dto, ct);

    [HttpPost("{id:int}/received")]
    public Task<OrderDto> Received(int id, OrderReceivedDto dto, CancellationToken ct) => service.MarkReceivedAsync(id, dto, ct);

    /// <summary>A partial delivery: stop waiting for the rest.</summary>
    [HttpPost("{id:int}/close")]
    public Task<OrderDto> Close(int id, CancellationToken ct) => service.CloseIncompleteAsync(id, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<OrderDto> Cancel(int id, CancellationToken ct) => service.CancelAsync(id, ct);
}
