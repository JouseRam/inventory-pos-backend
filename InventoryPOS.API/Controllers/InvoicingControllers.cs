using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Invoicing;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace InventoryPOS.API.Controllers;

/// <summary>This computer's account with the invoicing provider and the business's CSD.</summary>
[ApiController]
[Route("api/invoicing/account")]
[RequireModule(PosModules.Settings)]
public class InvoicingAccountController(InvoicingAccountService service) : ControllerBase
{
    private const long MaxCsdFileBytes = 64 * 1024;

    /// <summary>The active account, or the saved one of the provider asked for.</summary>
    [HttpGet]
    public Task<InvoicingAccountDto> Get([FromQuery] string? provider, CancellationToken ct) => service.GetAsync(provider, ct);

    [HttpPut]
    public Task<InvoicingAccountDto> Save(InvoicingAccountSaveDto dto, CancellationToken ct) => service.SaveAsync(dto, ct);

    /// <summary>Tries the saved credentials against the provider.</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromQuery] string? provider, CancellationToken ct)
    {
        await service.VerifyAsync(provider, ct);
        return Ok(new { ok = true });
    }

    /// <summary>The .cer, the .key and the key's password; the files go to the provider and are not stored here.</summary>
    [HttpPost("csd")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<InvoicingAccountDto> UploadCsd([FromForm] IFormFile? certificate, [FromForm] IFormFile? privateKey,
        [FromForm] string? password, CancellationToken ct)
    {
        if (certificate is null || privateKey is null || string.IsNullOrEmpty(password))
            throw new DomainException("Selecciona el archivo .cer, el archivo .key y captura la contraseña de la llave privada.");
        if (certificate.Length > MaxCsdFileBytes || privateKey.Length > MaxCsdFileBytes)
            throw new DomainException("Los archivos del CSD pesan unos pocos KB; revisa que sean el .cer y el .key correctos.");
        return await service.UploadCsdAsync(await ReadAsync(certificate, ct), await ReadAsync(privateKey, ct), password, ct);
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return stream.ToArray();
    }
}

[ApiController]
[Route("api/invoices")]
[RequireModule(PosModules.Invoicing)]
public class InvoicesController(InvoiceService service) : ControllerBase
{
    /// <summary>Data to prefill the invoice dialog of a sale, its current invoice and what setup is missing.</summary>
    [HttpGet("draft/{saleId:int}")]
    public Task<InvoiceDraftDto> Draft(int saleId, CancellationToken ct) => service.DraftAsync(saleId, ct);

    [HttpGet("by-sale/{saleId:int}")]
    public async Task<IActionResult> BySale(int saleId, CancellationToken ct) =>
        await service.GetActiveForSaleAsync(saleId, ct) is { } invoice ? Ok(invoice) : NoContent();

    [HttpPost("sale/{saleId:int}")]
    public Task<InvoiceDto> Create(int saleId, InvoiceCreateDto dto, CancellationToken ct) => service.CreateAsync(saleId, dto, ct);

    [HttpGet("{id:int}/pdf")]
    public Task<IActionResult> Pdf(int id, CancellationToken ct) => FileAsync(id, CfdiFileFormat.Pdf, ct);

    [HttpGet("{id:int}/xml")]
    public Task<IActionResult> Xml(int id, CancellationToken ct) => FileAsync(id, CfdiFileFormat.Xml, ct);

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, InvoiceCancelDto dto, CancellationToken ct)
    {
        var (invoice, message) = await service.CancelAsync(id, dto, ct);
        return Ok(new { invoice, message });
    }

    private async Task<IActionResult> FileAsync(int id, CfdiFileFormat format, CancellationToken ct)
    {
        var (content, name, type) = await service.FileAsync(id, format, ct);
        return File(content, type, name);
    }
}
