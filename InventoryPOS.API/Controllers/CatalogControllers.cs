using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Catalog;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/brands")]
[RequireModule(PosModules.Products)]
public class BrandsController(BrandService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BrandDto>> GetAll([FromQuery] bool includeInactive, CancellationToken ct) => service.ListAsync(includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<BrandDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(BrandSaveDto dto, CancellationToken ct)
    {
        var brand = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = brand.Id }, brand);
    }

    [HttpPut("{id:int}")]
    public Task<BrandDto> Update(int id, BrandSaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/categories")]
[RequireModule(PosModules.Products)]
public class CategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> GetAll([FromQuery] bool includeInactive, CancellationToken ct) => service.ListAsync(includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<CategoryDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(CategorySaveDto dto, CancellationToken ct)
    {
        var category = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
    }

    [HttpPut("{id:int}")]
    public Task<CategoryDto> Update(int id, CategorySaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/suppliers")]
[RequireModule(PosModules.Suppliers)]
public class SuppliersController(SupplierService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SupplierDto>> GetAll([FromQuery] string? q, [FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(q, includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<SupplierDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(SupplierSaveDto dto, CancellationToken ct)
    {
        var supplier = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:int}")]
    public Task<SupplierDto> Update(int id, SupplierSaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/customers")]
[RequireModule(PosModules.Customers)]
public class CustomersController(CustomerService service) : ControllerBase
{
    /// <summary>Cashiers pick customers at the POS, so listing is open to the Sales module too.</summary>
    [HttpGet]
    [RequireModule(PosModules.Sales)]
    public Task<IReadOnlyList<CustomerDto>> GetAll([FromQuery] string? q, [FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(q, includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Create(CustomerSaveDto dto, CancellationToken ct)
    {
        var customer = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    public Task<CustomerDto> Update(int id, CustomerSaveDto dto, CancellationToken ct) => service.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
