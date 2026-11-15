using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Promotions;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/promotions")]
[RequireModule(PosModules.Promotions)]
public class PromotionsController(PromotionService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PromotionSummaryDto>> GetAll(CancellationToken ct) => service.ListAsync(ct);

    [HttpGet("{id:int}")]
    public Task<PromotionDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    [RequestSizeLimit(3_000_000)]
    public Task<PromotionDto> Create(PromotionSaveDto dto, CancellationToken ct) => service.CreateAsync(dto, ct);

    [HttpPut("{id:int}")]
    [RequestSizeLimit(3_000_000)]
    public Task<PromotionDto> Update(int id, PromotionSaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> Image(int id, CancellationToken ct)
    {
        var (content, type) = await service.ImageAsync(id, ct);
        return File(content, type);
    }

    /// <summary>Customers who may receive promotions (with phone and consent).</summary>
    [HttpGet("audience")]
    public Task<AudienceDto> Audience([FromQuery] string? type, [FromQuery] string? q, CancellationToken ct) => service.AudienceAsync(type, q, ct);

    [HttpPost("{id:int}/recipients")]
    public Task<PromotionDto> AddRecipients(int id, RecipientsAddDto dto, CancellationToken ct) => service.AddRecipientsAsync(id, dto, ct);

    [HttpPut("{id:int}/recipients/{recipientId:int}")]
    public Task<PromotionRecipientDto> SetStatus(int id, int recipientId, RecipientStatusDto dto, CancellationToken ct) =>
        service.SetStatusAsync(id, recipientId, dto, ct);

    [HttpDelete("{id:int}/recipients/{recipientId:int}")]
    public async Task<IActionResult> RemoveRecipient(int id, int recipientId, CancellationToken ct)
    {
        await service.RemoveRecipientAsync(id, recipientId, ct);
        return NoContent();
    }
}
