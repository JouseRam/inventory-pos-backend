using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/products")]
[RequireModule(PosModules.Products)]
public class ProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProductDto>> GetAll([FromQuery] string? q, [FromQuery] int? categoryId, [FromQuery] int? brandId,
        [FromQuery] bool includeInactive, [FromQuery] bool lowStock, CancellationToken ct) =>
        service.ListAsync(q, categoryId, brandId, includeInactive, lowStock, ct);

    [HttpGet("{id:int}")]
    public Task<ProductDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(ProductCreateDto dto, CancellationToken ct)
    {
        var product = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public Task<ProductDto> Update(int id, ProductUpdateDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/variants")]
    public Task<ProductDto> AddVariant(int id, VariantSaveDto dto, CancellationToken ct) => service.AddVariantAsync(id, dto, ct);
}

/// <summary>Sellable variants: lookups serve the POS, inventory and receiving screens, so any signed-in user may run them.</summary>
[ApiController]
[Route("api/variants")]
[RequireModule(PosModules.Products)]
public class VariantsController(ProductService service) : ControllerBase
{
    [HttpGet("search")]
    [AnyUser]
    public Task<IReadOnlyList<VariantDto>> Search([FromQuery] string? q, CancellationToken ct) => service.SearchVariantsAsync(q, ct);

    [HttpGet("code/{code}")]
    [AnyUser]
    public Task<VariantDto> GetByCode(string code, CancellationToken ct) => service.GetVariantByCodeAsync(code, ct);

    [HttpGet("low-stock")]
    [RequireModule(PosModules.Inventory)]
    public Task<IReadOnlyList<VariantDto>> LowStock(CancellationToken ct) => service.LowStockAsync(ct);

    [HttpPut("{id:int}")]
    public Task<VariantDto> Update(int id, VariantSaveDto dto, CancellationToken ct) => service.UpdateVariantAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteVariantAsync(id, ct);
        return NoContent();
    }
}
