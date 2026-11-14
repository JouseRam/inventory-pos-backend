using FluentValidation;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Invoicing;

public record InvoiceDto(
    int Id, int SaleId, string SaleFolio, string Uuid, string Status, string ReceiverRfc, string ReceiverName, string CfdiUse,
    string PaymentForm, decimal Total, bool Sandbox, DateTime CreatedAt, DateTime? CancelledAt, string? CancelMotive);

/// <summary>
/// The receiver's fiscal data as captured at the register. It is saved on the customer (the sale's, the one given,
/// or one found or created by RFC) so the next invoice is filled in. PaymentForm null takes the sale's main payment.
/// </summary>
public record InvoiceCreateDto(
    int? CustomerId, string TaxId, string LegalName, string TaxRegime, string TaxZipCode, string CfdiUse,
    string? PaymentForm = null, string? Email = null);

public record InvoiceCancelDto(string Motive, string? ReplacementUuid);

/// <summary>What the invoice dialog needs: the receiver suggested for the sale and the default payment form.</summary>
public record InvoiceDraftDto(int SaleId, string SaleFolio, decimal Total, int? CustomerId, string? CustomerName,
    string? TaxId, string? LegalName, string? TaxRegime, string? TaxZipCode, string? CfdiUse, string PaymentForm,
    InvoiceDto? Invoice, IReadOnlyList<string> MissingSetup, bool Sandbox);

public class InvoiceCreateValidator : AbstractValidator<InvoiceCreateDto>
{
    public InvoiceCreateValidator()
    {
        RuleFor(x => FiscalRules.Normalize(x.TaxId)).Must(FiscalRules.IsValidRfc).OverridePropertyName(nameof(InvoiceCreateDto.TaxId))
            .WithMessage("RFC inválido: 12 caracteres para persona moral o 13 para persona física.");
        RuleFor(x => FiscalRules.Normalize(x.TaxId)).NotEqual(FiscalRules.GeneralPublicRfc).OverridePropertyName(nameof(InvoiceCreateDto.TaxId))
            .WithMessage("Las ventas al público en general van en la factura global, no en una factura individual.");
        RuleFor(x => x.LegalName).NotEmpty().WithMessage("Captura el nombre o razón social.").MaximumLength(300);
        RuleFor(x => x.TaxRegime).Must((x, code) => FiscalRules.IsValidTaxRegime(code, FiscalRules.Normalize(x.TaxId)))
            .WithMessage("Elige un régimen fiscal que corresponda al tipo de RFC.");
        RuleFor(x => x.TaxZipCode).Must(z => FiscalRules.IsValidZip(z?.Trim())).WithMessage("El código postal fiscal debe tener 5 dígitos.");
        RuleFor(x => FiscalRules.Normalize(x.CfdiUse)).Must((x, code) => FiscalRules.IsValidCfdiUse(code, FiscalRules.Normalize(x.TaxId)))
            .OverridePropertyName(nameof(InvoiceCreateDto.CfdiUse)).WithMessage("Elige un uso de CFDI que corresponda al tipo de RFC.");
        RuleFor(x => x.PaymentForm).Must(f => SatCatalogs.PaymentForms.Any(p => p.Code == f))
            .When(x => !string.IsNullOrEmpty(x.PaymentForm)).WithMessage("Forma de pago inválida.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("Correo inválido.");
    }
}

public class InvoiceCancelValidator : AbstractValidator<InvoiceCancelDto>
{
    public InvoiceCancelValidator()
    {
        RuleFor(x => x.Motive).Must(m => CancelMotives.All.Any(c => c.Code == m)).WithMessage("Elige el motivo de cancelación.");
        RuleFor(x => x.ReplacementUuid).NotEmpty().When(x => x.Motive == CancelMotives.WithErrorsReplaced)
            .WithMessage("El motivo 01 requiere el folio fiscal (UUID) de la factura que sustituye a esta.");
    }
}

public class InvoiceService(
    IUnitOfWork uow, InvoicingAccountService accounts, SettingService settings, ICurrentUser currentUser, IPosEnvironment env)
{
    private IRepository<Invoice> Invoices => uow.Repository<Invoice>();

    /// <summary>Prefills the invoice dialog and tells what configuration is still missing.</summary>
    public async Task<InvoiceDraftDto> DraftAsync(int saleId, CancellationToken ct = default)
    {
        var sale = await uow.Sales.GetWithDetailsAsync(saleId, ct) ?? throw NotFoundException.For("Venta", saleId);
        var invoice = await Invoices.FirstOrDefaultAsync(i => i.SaleId == saleId && i.Status == InvoiceStatus.Active, ct);
        var account = await accounts.GetAsync(null, ct);
        var c = sale.Customer;
        return new InvoiceDraftDto(sale.Id, sale.Folio, sale.Total, c?.Id, c?.Name, c?.TaxId, c?.LegalName, c?.TaxRegime, c?.TaxZipCode,
            c?.CfdiUse, MainPaymentForm(sale), invoice == null ? null : ToDto(invoice, sale.Folio),
            await MissingSetupAsync(account, ct), account.Sandbox);
    }

    public async Task<InvoiceDto?> GetActiveForSaleAsync(int saleId, CancellationToken ct = default)
    {
        var invoice = await Invoices.FirstOrDefaultAsync(i => i.SaleId == saleId && i.Status == InvoiceStatus.Active, ct);
        if (invoice == null) return null;
        var sale = await uow.Sales.GetByIdAsync(saleId, ct);
        return ToDto(invoice, sale?.Folio ?? "");
    }

    /// <summary>Stamps the sale's CFDI. Needs internet; the sale itself is never touched.</summary>
    public async Task<InvoiceDto> CreateAsync(int saleId, InvoiceCreateDto dto, CancellationToken ct = default)
    {
        if (env.Role == PosRole.Central)
            throw new DomainException("Por ahora las facturas se emiten desde la caja donde se hizo la venta.");
        var sale = await uow.Sales.GetWithDetailsAsync(saleId, ct) ?? throw NotFoundException.For("Venta", saleId);
        if (sale.Status != SaleStatus.Completed) throw new DomainException("Solo se facturan ventas completadas.");
        if (await Invoices.AnyAsync(i => i.SaleId == saleId && i.Status == InvoiceStatus.Active, ct))
            throw new DomainException("Esta venta ya está facturada.");

        var accountDto = await accounts.GetAsync(null, ct);
        var missing = await MissingSetupAsync(accountDto, ct);
        if (missing.Count > 0) throw new DomainException("Falta configurar la facturación: " + string.Join("; ", missing) + ".");
        var (account, provider, credentials) = await accounts.RequireCredentialsAsync(null, ct);

        var receiver = new CfdiParty(FiscalRules.Normalize(dto.TaxId)!, FiscalRules.Normalize(dto.LegalName)!, dto.TaxRegime.Trim(), dto.TaxZipCode.Trim());
        var cfdiUse = FiscalRules.Normalize(dto.CfdiUse)!;
        await RememberReceiverAsync(sale, dto.CustomerId, receiver, cfdiUse, dto.Email, ct);

        var issuer = new CfdiParty(
            FiscalRules.Normalize(await settings.GetAsync(SettingKeys.BusinessRfc, ct))!,
            FiscalRules.Normalize(await settings.GetAsync(SettingKeys.BusinessLegalName, ct))!,
            (await settings.GetAsync(SettingKeys.BusinessTaxRegime, ct))!,
            (await settings.GetAsync(SettingKeys.BusinessZipCode, ct))!);
        var paymentForm = string.IsNullOrEmpty(dto.PaymentForm) ? MainPaymentForm(sale) : dto.PaymentForm;
        var request = new CfdiRequest(issuer, receiver, cfdiUse, account.Serie, sale.Folio, paymentForm,
            await ConceptsAsync(sale, account.DefaultProductCode, ct));

        var stamped = await provider.StampAsync(credentials, request, ct);
        var invoice = new Invoice
        {
            SaleId = sale.Id, Provider = provider.Name, ProviderId = stamped.ProviderId, Uuid = stamped.Uuid,
            ReceiverRfc = receiver.Rfc, ReceiverName = receiver.Name, CfdiUse = cfdiUse, PaymentForm = paymentForm,
            Total = stamped.Total > 0 ? stamped.Total : sale.Total, Sandbox = account.Sandbox, UserId = currentUser.UserId,
        };
        Invoices.Add(invoice);
        await uow.SaveChangesAsync(ct);

        // The files are fetched now so the invoice can be reprinted offline; if this fails they are fetched on demand.
        try
        {
            invoice.Xml = System.Text.Encoding.UTF8.GetString(await provider.DownloadAsync(credentials, invoice.ProviderId, CfdiFileFormat.Xml, ct));
            invoice.Pdf = await provider.DownloadAsync(credentials, invoice.ProviderId, CfdiFileFormat.Pdf, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch (InvoicingException) { }

        return ToDto(invoice, sale.Folio);
    }

    /// <summary>The stored XML or PDF; fetched from the provider (and kept) when it was not downloaded yet.</summary>
    public async Task<(byte[] Content, string FileName, string ContentType)> FileAsync(int invoiceId, CfdiFileFormat format, CancellationToken ct = default)
    {
        var invoice = await Invoices.GetByIdAsync(invoiceId, ct) ?? throw NotFoundException.For("Factura", invoiceId);
        var stored = format == CfdiFileFormat.Pdf ? invoice.Pdf : invoice.Xml is null ? null : System.Text.Encoding.UTF8.GetBytes(invoice.Xml);
        if (stored == null)
        {
            var (_, provider, credentials) = await accounts.RequireCredentialsAsync(invoice.Provider, ct);
            stored = await provider.DownloadAsync(credentials, invoice.ProviderId, format, ct);
            if (format == CfdiFileFormat.Pdf) invoice.Pdf = stored;
            else invoice.Xml = System.Text.Encoding.UTF8.GetString(stored);
            await uow.SaveChangesAsync(ct);
        }
        var name = $"{invoice.Uuid}.{(format == CfdiFileFormat.Pdf ? "pdf" : "xml")}";
        return (stored, name, format == CfdiFileFormat.Pdf ? "application/pdf" : "application/xml");
    }

    /// <summary>
    /// Asks the SAT, through the provider, to cancel. Some receivers must accept the cancellation; until they do the
    /// invoice stays active and the answer says so.
    /// </summary>
    public async Task<(InvoiceDto Invoice, string Message)> CancelAsync(int invoiceId, InvoiceCancelDto dto, CancellationToken ct = default)
    {
        var invoice = await Invoices.GetByIdAsync(invoiceId, ct) ?? throw NotFoundException.For("Factura", invoiceId);
        if (invoice.Status == InvoiceStatus.Cancelled) throw new DomainException("La factura ya está cancelada.");
        var (_, provider, credentials) = await accounts.RequireCredentialsAsync(invoice.Provider, ct);

        var outcome = await provider.CancelAsync(credentials, invoice.ProviderId, dto.Motive, dto.ReplacementUuid?.Trim(), ct);
        string message;
        if (outcome.Cancelled)
        {
            invoice.Status = InvoiceStatus.Cancelled;
            invoice.CancelMotive = dto.Motive;
            invoice.CancelledAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync(ct);
            message = "Factura cancelada ante el SAT.";
        }
        else
        {
            message = "La solicitud de cancelación se envió; el receptor debe aceptarla. Estado: " + outcome.Status +
                (string.IsNullOrWhiteSpace(outcome.Message) ? "" : $" ({outcome.Message})");
        }
        var sale = await uow.Sales.GetByIdAsync(invoice.SaleId, ct);
        return (ToDto(invoice, sale?.Folio ?? ""), message);
    }

    private async Task<IReadOnlyList<CfdiConcept>> ConceptsAsync(Sale sale, string productCode, CancellationToken ct)
    {
        var rate = await settings.GetDecimalAsync(SettingKeys.TaxRate, 16m, ct);
        var items = sale.Items.OrderBy(i => i.Id).ToList();
        var variants = (await uow.Variants.GetByIdsWithProductAsync(items.Select(i => i.VariantId).Distinct(), ct)).ToDictionary(v => v.Id);
        var amounts = CfdiCalculator.Calculate(items.Select(i => new TaxInclusiveLine(i.Quantity, i.UnitPrice, i.Discount)).ToList(), sale.Discount, rate);

        return items.Select((item, n) =>
        {
            var line = amounts.Lines[n];
            variants.TryGetValue(item.VariantId, out var variant);
            var (unitCode, unitName) = SatCatalogs.UnitFor(variant?.Unit ?? UnitsOfMeasure.Piece);
            return new CfdiConcept(productCode, unitCode, unitName, variant?.SKU, item.Description,
                line.Quantity, line.UnitValue, line.Amount, line.Discount, line.Base, rate / 100m, line.Tax, line.Total);
        }).ToList();
    }

    /// <summary>Keeps the receiver's data on a customer so the next invoice is prefilled.</summary>
    private async Task RememberReceiverAsync(Sale sale, int? customerId, CfdiParty receiver, string cfdiUse, string? email, CancellationToken ct)
    {
        var customers = uow.Repository<Customer>();
        var customer = customerId.HasValue ? await customers.GetByIdAsync(customerId.Value, ct)
            : sale.CustomerId.HasValue ? await customers.GetByIdAsync(sale.CustomerId.Value, ct)
            : await customers.FirstOrDefaultAsync(c => c.TaxId == receiver.Rfc, ct);
        if (customer == null) customers.Add(customer = new Customer { Name = receiver.Name });
        else customer.UpdatedAt = DateTime.UtcNow;

        customer.TaxId = receiver.Rfc;
        customer.LegalName = receiver.Name;
        customer.TaxRegime = receiver.TaxRegime;
        customer.TaxZipCode = receiver.ZipCode;
        customer.CfdiUse = cfdiUse;
        if (!string.IsNullOrWhiteSpace(email)) customer.Email = email.Trim();
        await uow.SaveChangesAsync(ct);
    }

    private async Task<List<string>> MissingSetupAsync(InvoicingAccountDto account, CancellationToken ct)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(await settings.GetAsync(SettingKeys.BusinessRfc, ct))) missing.Add("RFC del negocio");
        if (string.IsNullOrEmpty(await settings.GetAsync(SettingKeys.BusinessLegalName, ct))) missing.Add("razón social del negocio");
        if (string.IsNullOrEmpty(await settings.GetAsync(SettingKeys.BusinessTaxRegime, ct))) missing.Add("régimen fiscal del negocio");
        if (string.IsNullOrEmpty(await settings.GetAsync(SettingKeys.BusinessZipCode, ct))) missing.Add("código postal de expedición");
        if (!account.Active || !account.HasPassword || account.PasswordUnreadable) missing.Add("cuenta del proveedor de facturación");
        else if (!account.CsdReady) missing.Add("certificado de sello digital (CSD)");
        return missing;
    }

    private static string MainPaymentForm(Sale sale)
    {
        var main = sale.Payments.GroupBy(p => p.Method).OrderByDescending(g => g.Sum(p => p.Amount)).FirstOrDefault();
        return SatCatalogs.PaymentFormFor(main?.Key ?? PaymentMethods.Cash);
    }

    private static InvoiceDto ToDto(Invoice i, string saleFolio) =>
        new(i.Id, i.SaleId, saleFolio, i.Uuid, i.Status, i.ReceiverRfc, i.ReceiverName, i.CfdiUse, i.PaymentForm, i.Total, i.Sandbox,
            i.CreatedAt, i.CancelledAt, i.CancelMotive);
}
