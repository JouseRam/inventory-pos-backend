using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Invoicing;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using InventoryPOS.Infrastructure.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InventoryPOS.Application.Tests;

/// <summary>Records what the module asks of the provider, without calling any PAC.</summary>
public class FakeInvoiceProvider : IInvoiceProvider
{
    public string Name { get; init; } = "Fake";
    public bool UploadsCsd { get; init; } = true;
    public ProviderInfo Info => new(Name, "Proveedor de prueba", UploadsCsd ? "Usuario" : null, "Contraseña", null, UploadsCsd, null, null,
        "Sin costo", [], "https://example.com/pay", "https://example.com", "https://example.com/pricing");
    public List<CfdiRequest> Stamped { get; } = [];
    public List<CsdFiles> Uploaded { get; } = [];
    public List<string> Cancelled { get; } = [];
    public bool Offline { get; set; }

    public Task VerifyAsync(ProviderAccount account, CancellationToken ct) => Task.CompletedTask;

    public Task UploadCsdAsync(ProviderAccount account, CsdFiles csd, CancellationToken ct)
    {
        Uploaded.Add(csd);
        return Task.CompletedTask;
    }

    public Task<StampedCfdi> StampAsync(ProviderAccount account, CfdiRequest request, CancellationToken ct)
    {
        if (Offline) throw new InvoicingOfflineException();
        Stamped.Add(request);
        return Task.FromResult(new StampedCfdi($"{Name}-{Stamped.Count}", Guid.NewGuid().ToString().ToUpperInvariant(),
            request.Concepts.Sum(c => c.Total)));
    }

    public Task<byte[]> DownloadAsync(ProviderAccount account, string providerId, CfdiFileFormat format, CancellationToken ct) =>
        Task.FromResult(Encoding.UTF8.GetBytes(format == CfdiFileFormat.Xml ? "<cfdi:Comprobante/>" : "%PDF-1.4"));

    public Task<CancelOutcome> CancelAsync(ProviderAccount account, string providerId, string motive, string? replacementUuid, CancellationToken ct) =>
        Task.FromResult(Cancel(providerId));

    private CancelOutcome Cancel(string providerId)
    {
        Cancelled.Add(providerId);
        return new CancelOutcome(true, "canceled", null);
    }
}

public class InvoicingTests
{
    private const string BusinessRfc = "EKU9003173C9";

    private sealed class Setup : IDisposable
    {
        public PosTestContext Pos { get; } = new();
        public FakeInvoiceProvider Provider { get; } = new();
        /// <summary>A second provider that, like Facturapi, receives the CSD in its own portal.</summary>
        public FakeInvoiceProvider Other { get; } = new() { Name = "Otro", UploadsCsd = false };
        public InvoicingAccountService Accounts { get; }
        public InvoiceService Invoices { get; }
        public SettingService Settings { get; }

        public Setup()
        {
            var protector = new AesSecretProtector(Options.Create(new JwtOptions { Key = new string('k', 48) }));
            Settings = new SettingService(Pos.Uow, new PosGuard(Pos.Env));
            Accounts = new InvoicingAccountService(Pos.Uow, [Provider, Other], protector, Settings);
            Invoices = new InvoiceService(Pos.Uow, Accounts, Settings, Pos.User, Pos.Env);
        }

        public async Task ConfigureAsync(bool withCsd = true)
        {
            void Add(string key, string? value) => Pos.Db.Settings.Add(new Setting { Key = key, Value = value, Label = key, Type = "text", Group = "Facturación" });
            Add(SettingKeys.BusinessRfc, BusinessRfc);
            Add(SettingKeys.BusinessLegalName, "ESCUELA KEMPER URGATE");
            Add(SettingKeys.BusinessTaxRegime, "601");
            Add(SettingKeys.BusinessZipCode, "42501");
            await Pos.Db.SaveChangesAsync();
            await Accounts.SaveAsync(new InvoicingAccountSaveDto("Fake", true, "usuario", "secreto", "53131600", "F"));
            if (withCsd)
            {
                var (cer, key) = Csd(BusinessRfc, "12345678a");
                await Accounts.UploadCsdAsync(cer, key, "12345678a");
            }
        }

        /// <summary>A sale of two dyes with an odd price, a line discount and a general discount.</summary>
        public async Task<SaleDto> SellAsync()
        {
            var dye = await Pos.CreateDyeAsync();
            await Pos.ReceiveAsync(dye.Id, ("A", "2027-01-31", 10));
            await Pos.Cash.OpenAsync(new CashOpenDto(0, null));
            return await Pos.Sales.CreateAsync(new SaleCreateDto(null, 3.5m, null,
                [new SaleItemSaveDto(dye.Id, 2, 189.9m, 10m), new SaleItemSaveDto(dye.Id, 1, null)],
                [new SalePaymentSaveDto(PaymentMethods.Card, 500, null), new SalePaymentSaveDto(PaymentMethods.Cash, 100, null)]));
        }

        public void Dispose() => Pos.Dispose();
    }

    private static InvoiceCreateDto Receiver(string rfc = "XOJI740919U48", string regime = "612", string use = "G03") =>
        new(null, rfc, "Ingrid Xodar Jimenez", regime, "88965", use);

    /// <summary>A self-signed certificate shaped like a SAT CSD, with its encrypted PKCS#8 key.</summary>
    internal static (byte[] Cer, byte[] Key) Csd(string rfc, string password, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName("ESCUELA KEMPER URGATE");
        subject.Add("2.5.4.45", $"{rfc} / VADA800927DJ3", UniversalTagNumber.PrintableString);
        var request = new CertificateRequest(subject.Build(), rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var serial = Encoding.ASCII.GetBytes("30001000000500003416");
        var end = notAfter ?? DateTimeOffset.UtcNow.AddYears(4);
        using var cert = request.Create(request.SubjectName, X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1),
            end.AddYears(-4), end, serial);
        var key = rsa.ExportEncryptedPkcs8PrivateKey(password,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2048));
        return (cert.RawData, key);
    }

    [Fact]
    public void A_csd_is_read_and_checked_against_its_key_and_password()
    {
        var (cer, key) = Csd(BusinessRfc, "12345678a");

        var info = InvoicingAccountService.ReadCsd(cer, key, "12345678a", DateTime.UtcNow);

        Assert.Equal(BusinessRfc, info.Rfc);
        Assert.Equal("30001000000500003416", info.CertificateNumber);
        var wrongPassword = Assert.Throws<DomainException>(() => InvoicingAccountService.ReadCsd(cer, key, "otra", DateTime.UtcNow));
        Assert.Contains("contraseña", wrongPassword.Message);
        var (_, otherKey) = Csd(BusinessRfc, "12345678a");
        Assert.Throws<DomainException>(() => InvoicingAccountService.ReadCsd(cer, otherKey, "12345678a", DateTime.UtcNow));
    }

    [Fact]
    public async Task A_csd_of_another_rfc_is_not_uploaded()
    {
        using var s = new Setup();
        await s.ConfigureAsync(withCsd: false);
        var (cer, key) = Csd("XOJI740919U48", "pass1234");

        var error = await Assert.ThrowsAsync<DomainException>(() => s.Accounts.UploadCsdAsync(cer, key, "pass1234"));

        Assert.Contains(BusinessRfc, error.Message);
        Assert.Empty(s.Provider.Uploaded);
    }

    [Fact]
    public async Task The_password_is_stored_encrypted_and_never_returned()
    {
        using var s = new Setup();
        await s.ConfigureAsync();

        var stored = await s.Pos.Db.Set<InvoicingAccount>().AsNoTracking().SingleAsync();
        Assert.DoesNotContain("secreto", stored.ProtectedPassword);
        var dto = await s.Accounts.GetAsync();
        Assert.True(dto.HasPassword);
        Assert.True(dto.Ready);
        Assert.DoesNotContain("secreto", System.Text.Json.JsonSerializer.Serialize(dto));
    }

    [Fact]
    public async Task Invoicing_a_sale_sends_concepts_that_add_up_to_what_was_charged()
    {
        using var s = new Setup();
        await s.ConfigureAsync();
        var sale = await s.SellAsync();

        var invoice = await s.Invoices.CreateAsync(sale.Id, Receiver());

        var request = Assert.Single(s.Provider.Stamped);
        Assert.Equal(sale.Total, request.Concepts.Sum(c => c.Total));
        Assert.Equal(BusinessRfc, request.Issuer.Rfc);
        Assert.Equal("42501", request.Issuer.ZipCode);
        Assert.Equal("XOJI740919U48", request.Receiver.Rfc);
        Assert.Equal("INGRID XODAR JIMENEZ", request.Receiver.Name);
        Assert.Equal("04", request.PaymentForm); // the card covered most of it
        Assert.Equal("F", request.Serie);
        Assert.Equal(sale.Folio, request.Folio);
        Assert.All(request.Concepts, c =>
        {
            Assert.Equal("53131600", c.ProductCode);
            Assert.Equal("MLT", c.UnitCode);
            Assert.Equal(0.16m, c.TaxRate);
        });
        Assert.Equal(InvoiceStatus.Active, invoice.Status);
        Assert.True(invoice.Sandbox);

        var (pdf, name, _) = await s.Invoices.FileAsync(invoice.Id, CfdiFileFormat.Pdf);
        Assert.Equal($"{invoice.Uuid}.pdf", name);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task The_receiver_is_remembered_for_the_next_invoice()
    {
        using var s = new Setup();
        await s.ConfigureAsync();
        var sale = await s.SellAsync();

        await s.Invoices.CreateAsync(sale.Id, Receiver());

        var customer = await s.Pos.Db.Customers.AsNoTracking().SingleAsync(c => c.TaxId == "XOJI740919U48");
        Assert.Equal("612", customer.TaxRegime);
        Assert.Equal("88965", customer.TaxZipCode);
    }

    [Fact]
    public async Task A_sale_is_invoiced_once_and_cannot_be_cancelled_while_invoiced()
    {
        using var s = new Setup();
        await s.ConfigureAsync();
        var sale = await s.SellAsync();
        var invoice = await s.Invoices.CreateAsync(sale.Id, Receiver());

        await Assert.ThrowsAsync<DomainException>(() => s.Invoices.CreateAsync(sale.Id, Receiver()));
        var blocked = await Assert.ThrowsAsync<DomainException>(() => s.Pos.Sales.CancelAsync(sale.Id, new SaleCancelDto("Error")));
        Assert.Contains("factura", blocked.Message);

        var (cancelled, _) = await s.Invoices.CancelAsync(invoice.Id, new InvoiceCancelDto(CancelMotives.WithErrorsNoReplacement, null));
        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        await s.Pos.Sales.CancelAsync(sale.Id, new SaleCancelDto("Error"));
    }

    [Fact]
    public async Task Missing_setup_is_explained_before_calling_the_provider()
    {
        using var s = new Setup();
        await s.ConfigureAsync(withCsd: false);
        var sale = await s.SellAsync();

        var draft = await s.Invoices.DraftAsync(sale.Id);
        Assert.Contains(draft.MissingSetup, m => m.Contains("CSD"));
        var error = await Assert.ThrowsAsync<DomainException>(() => s.Invoices.CreateAsync(sale.Id, Receiver()));
        Assert.Contains("CSD", error.Message);
        Assert.Empty(s.Provider.Stamped);
    }

    [Fact]
    public async Task Without_internet_no_invoice_is_recorded()
    {
        using var s = new Setup();
        await s.ConfigureAsync();
        var sale = await s.SellAsync();
        s.Provider.Offline = true;

        var error = await Assert.ThrowsAsync<InvoicingOfflineException>(() => s.Invoices.CreateAsync(sale.Id, Receiver()));

        Assert.Contains("internet", error.Message);
        Assert.False(await s.Pos.Db.Invoices.AnyAsync());
    }

    [Fact]
    public async Task The_web_does_not_invoice_yet()
    {
        using var s = new Setup();
        var central = new InvoiceService(s.Pos.Uow, s.Accounts, s.Settings, s.Pos.User, new FakeEnvironment(PosRole.Central));
        await s.ConfigureAsync();
        var sale = await s.SellAsync();

        await Assert.ThrowsAsync<DomainException>(() => central.CreateAsync(sale.Id, Receiver()));
    }

    [Theory]
    [InlineData("XAXX010101000", "616", "S01")] // general public goes in the global invoice
    [InlineData("XOJI740919U48", "601", "G03")] // persona moral regime for a persona física
    public void Invalid_receivers_are_rejected(string rfc, string regime, string use)
    {
        Assert.False(new InvoiceCreateValidator().Validate(Receiver(rfc, regime, use)).IsValid);
    }

    [Fact]
    public async Task Switching_provider_keeps_the_previous_invoices_working()
    {
        using var s = new Setup();
        await s.ConfigureAsync();
        var first = await s.SellAsync();
        var old = await s.Invoices.CreateAsync(first.Id, Receiver());

        // The second provider keeps the CSD in its portal: saving its key is enough to invoice.
        var other = await s.Accounts.SaveAsync(new InvoicingAccountSaveDto("Otro", false, "", "llave-otro", "53131600", null));
        Assert.True(other.Ready);
        Assert.Equal("Otro", other.ActiveProvider);
        var second = await s.Pos.Sales.CreateAsync(new SaleCreateDto(null, 0, null,
            [new SaleItemSaveDto(first.Items![0].VariantId, 1, null)], [new SalePaymentSaveDto(PaymentMethods.Cash, 500, null)]));
        await s.Invoices.CreateAsync(second.Id, Receiver());
        Assert.Single(s.Other.Stamped);

        // The first invoice is still served and cancelled by the provider that stamped it.
        await s.Invoices.FileAsync(old.Id, CfdiFileFormat.Xml);
        await s.Invoices.CancelAsync(old.Id, new InvoiceCancelDto(CancelMotives.WithErrorsNoReplacement, null));
        Assert.Equal(["Fake-1"], s.Provider.Cancelled);
        Assert.Empty(s.Other.Cancelled);

        // Both accounts stay saved; only one is active.
        var accounts = (await s.Accounts.GetAsync()).Accounts;
        Assert.Equal(2, accounts.Count);
        Assert.Single(accounts, a => a.Active);
        Assert.Equal("usuario", (await s.Accounts.GetAsync("Fake")).UserName);
    }

    [Fact]
    public async Task A_provider_that_keeps_the_csd_in_its_portal_does_not_take_it_here()
    {
        using var s = new Setup();
        await s.ConfigureAsync(withCsd: false);
        await s.Accounts.SaveAsync(new InvoicingAccountSaveDto("Otro", false, "", "llave-otro", "53131600", null));
        var (cer, key) = Csd(BusinessRfc, "12345678a");

        await Assert.ThrowsAsync<DomainException>(() => s.Accounts.UploadCsdAsync(cer, key, "12345678a"));
        Assert.Empty(s.Other.Uploaded);
    }

    [Theory]
    [InlineData("V-000123", 123L)]
    [InlineData("SIN-NUMERO", null)]
    public void Facturapi_folios_are_numbers(string folio, long? expected)
    {
        Assert.Equal(expected, FacturapiProvider.FolioNumber(folio));
    }

    [Fact]
    public void Facturapi_errors_are_turned_into_readable_messages()
    {
        Assert.Equal("El RFC no es válido.", FacturapiProvider.ErrorMessage("""{"message":"El RFC no es válido.","path":"customer.tax_id"}"""));
    }

    [Fact]
    public void Facturama_errors_are_turned_into_readable_messages()
    {
        var message = FacturamaProvider.ErrorMessage(
            """{"Message":"La solicitud no es válida.","ModelState":{"Receiver.Rfc":["El RFC del receptor no está en la lista de RFC inscritos."]}}""");

        Assert.Equal("El RFC del receptor no está en la lista de RFC inscritos.", message);
    }
}
