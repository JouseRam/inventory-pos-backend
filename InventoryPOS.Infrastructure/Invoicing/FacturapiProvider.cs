using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryPOS.Application.Features.Invoicing;

namespace InventoryPOS.Infrastructure.Invoicing;

/// <summary>
/// Facturapi (API v2) with an organization's secret key: the organization, its fiscal data and its CSD live in the
/// Facturapi dashboard, and the key itself says whether it is a test key (sk_test_) or a live one (sk_live_).
/// Endpoints and field names follow Facturapi's official .NET SDK.
/// </summary>
public class FacturapiProvider(IHttpClientFactory httpFactory) : IInvoiceProvider
{
    public const string ProviderName = "Facturapi";
    public const string HttpClientName = "Facturapi";
    private const string BaseUrl = "https://www.facturapi.io/v2/";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public string Name => ProviderName;

    public ProviderInfo Info { get; } = new(
        ProviderName, "Facturapi", null, "Llave secreta de la organización (API key)",
        "Empieza con sk_test_ (pruebas) o sk_live_ (facturas reales). La encuentras en el panel de Facturapi → Desarrolladores.",
        UploadsCsd: false,
        CsdPortalHint: "En Facturapi el CSD y los datos fiscales se cargan en su panel (Organización → Certificados).",
        TestKeyPrefix: "sk_test_",
        Billing: "Suscripción mensual más un cargo por factura, cobrados por Facturapi a tu tarjeta.",
        Plans:
        [
            new("API de facturación", "$299 MXN al mes", "Más consumo."),
            new("Timbre", "$0.60 MXN c/u", "Se cobra el mes siguiente."),
            new("Pruebas", "Gratis", "Con la llave sk_test_; las facturas no tienen validez."),
        ],
        BillingUrl: "https://dashboard.facturapi.io",
        SignupUrl: "https://dashboard.facturapi.io",
        PricingUrl: "https://www.facturapi.io/pricing");

    public async Task VerifyAsync(ProviderAccount account, CancellationToken ct)
    {
        using var response = await SendAsync(account, HttpMethod.Get, "invoices?limit=1", null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task UploadCsdAsync(ProviderAccount account, CsdFiles csd, CancellationToken ct) =>
        throw new InvoicingException("Con Facturapi el CSD se sube en su panel: Organización → Certificados.");

    public async Task<StampedCfdi> StampAsync(ProviderAccount account, CfdiRequest r, CancellationToken ct)
    {
        // Facturapi computes the IVA itself from the base: the amounts sent are before tax, as calculated for the SAT.
        var body = new
        {
            Customer = new
            {
                LegalName = r.Receiver.Name, TaxId = r.Receiver.Rfc, TaxSystem = r.Receiver.TaxRegime,
                Address = new { Zip = r.Receiver.ZipCode },
            },
            Items = r.Concepts.Select(c => new
            {
                Quantity = c.Quantity,
                Discount = c.Discount > 0 ? c.Discount : (decimal?)null,
                Product = new
                {
                    Description = c.Description,
                    ProductKey = c.ProductCode,
                    Price = c.UnitValue,
                    TaxIncluded = false,
                    UnitKey = c.UnitCode,
                    UnitName = c.UnitName,
                    Sku = c.Sku,
                    Taxes = new[] { new { Type = "IVA", Rate = c.TaxRate } },
                },
            }).ToList(),
            Use = r.CfdiUse,
            PaymentForm = r.PaymentForm,
            PaymentMethod = "PUE",
            Series = string.IsNullOrWhiteSpace(r.Serie) ? null : r.Serie,
            FolioNumber = FolioNumber(r.Folio),
        };

        using var response = await SendAsync(account, HttpMethod.Post, "invoices", body, ct);
        await EnsureSuccessAsync(response, ct);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json, ct)
            ?? throw new InvoicingException("Facturapi no devolvió la factura timbrada.");
        if (string.IsNullOrEmpty(invoice.Id) || string.IsNullOrEmpty(invoice.Uuid))
            throw new InvoicingException("Facturapi no devolvió el folio fiscal (UUID) de la factura.");
        return new StampedCfdi(invoice.Id, invoice.Uuid.ToUpperInvariant(), invoice.Total);
    }

    public async Task<byte[]> DownloadAsync(ProviderAccount account, string providerId, CfdiFileFormat format, CancellationToken ct)
    {
        var kind = format == CfdiFileFormat.Pdf ? "pdf" : "xml";
        using var response = await SendAsync(account, HttpMethod.Get, $"invoices/{Uri.EscapeDataString(providerId)}/{kind}", null, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<CancelOutcome> CancelAsync(ProviderAccount account, string providerId, string motive, string? replacementUuid, CancellationToken ct)
    {
        var url = $"invoices/{Uri.EscapeDataString(providerId)}?motive={Uri.EscapeDataString(motive)}" +
            (string.IsNullOrWhiteSpace(replacementUuid) ? "" : $"&substitution={Uri.EscapeDataString(replacementUuid)}");
        using var response = await SendAsync(account, HttpMethod.Delete, url, null, ct);
        await EnsureSuccessAsync(response, ct);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json, ct);
        var cancelled = string.Equals(invoice?.Status, "canceled", StringComparison.OrdinalIgnoreCase);
        // Receivers who must accept leave it "pending" until they answer.
        return new CancelOutcome(cancelled, invoice?.CancellationStatus ?? invoice?.Status ?? "desconocido", null);
    }

    /// <summary>Facturapi's folio is a number: the digits of the sale folio (V-000123 → 123).</summary>
    public static long? FolioNumber(string folio)
    {
        var digits = new string(folio.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var n) ? n : null;
    }

    private async Task<HttpResponseMessage> SendAsync(ProviderAccount account, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, new Uri(new Uri(BaseUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{account.Password}:")));
        if (body != null) request.Content = JsonContent.Create(body, options: Json);
        try
        {
            return await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvoicingOfflineException(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InvoicingOfflineException(ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvoicingException("Facturapi rechazó la llave secreta. Revísala en Configuración → Facturación.");
        if ((int)response.StatusCode >= 500)
            throw new InvoicingException("Facturapi no está respondiendo en este momento. Intenta de nuevo en unos minutos.");

        var text = await response.Content.ReadAsStringAsync(ct);
        throw new InvoicingException(ErrorMessage(text) ?? $"Facturapi rechazó la solicitud ({(int)response.StatusCode}).");
    }

    /// <summary>Facturapi answers errors with { message, path }.</summary>
    public static string? ErrorMessage(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(body, Json);
            return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message;
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(body) || body.Length > 300 ? null : body;
        }
    }

    private sealed record InvoiceResponse(string? Id, string? Uuid, decimal Total, string? Status, string? CancellationStatus);
    private sealed record ErrorResponse(string? Message, string? Path);
}
