using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryPOS.Application.Features.Invoicing;

namespace InventoryPOS.Infrastructure.Invoicing;

/// <summary>
/// Facturama's API Multiemisor ("api-lite"): the issuer's CSD is stored at Facturama, which builds, seals and stamps
/// the CFDI from JSON. Endpoints and field names follow Facturama's official .NET SDK.
/// </summary>
public class FacturamaProvider(IHttpClientFactory httpFactory) : IInvoiceProvider
{
    public const string ProviderName = "Facturama";
    public const string HttpClientName = "Facturama";
    private const string SandboxUrl = "https://apisandbox.facturama.mx/";
    private const string ProductionUrl = "https://api.facturama.mx/";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public string Name => ProviderName;

    public ProviderInfo Info { get; } = new(
        ProviderName, "Facturama (API Multiemisor)", "Usuario de Facturama", "Contraseña de Facturama", null,
        UploadsCsd: true, CsdPortalHint: null, TestKeyPrefix: null,
        Billing: "Pago anual del módulo API más folios en prepago, que compras en tu cuenta de Facturama cuando se acaban.",
        Plans:
        [
            new("Módulo API", "$1,650 MXN al año", "Incluye API Web y Multiemisor y 100 folios."),
            new("Folios", "$0.50 MXN c/u", "De 1 a 10,000; baja a $0.45 y $0.40 con más volumen. Vigencia anual."),
            new("Pruebas", "Gratis", "Ambiente de pruebas sin costo; las facturas no tienen validez."),
        ],
        BillingUrl: "https://facturama.mx/planes-facturacion",
        SignupUrl: "https://facturama.mx/api-facturacion-electronica",
        PricingUrl: "https://api.facturama.mx/costos");

    public async Task VerifyAsync(ProviderAccount account, CancellationToken ct)
    {
        using var response = await SendAsync(account, HttpMethod.Get, "api-lite/csds/", null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task UploadCsdAsync(ProviderAccount account, CsdFiles csd, CancellationToken ct)
    {
        var body = new
        {
            Rfc = csd.Rfc,
            Certificate = Convert.ToBase64String(csd.Certificate),
            PrivateKey = Convert.ToBase64String(csd.PrivateKey),
            PrivateKeyPassword = csd.Password,
        };
        // An RFC already registered is replaced; a new one is added.
        using var existing = await SendAsync(account, HttpMethod.Get, $"api-lite/csds/{Uri.EscapeDataString(csd.Rfc)}", null, ct);
        using var response = existing.IsSuccessStatusCode
            ? await SendAsync(account, HttpMethod.Put, $"api-lite/csds/{Uri.EscapeDataString(csd.Rfc)}", body, ct)
            : await SendAsync(account, HttpMethod.Post, "api-lite/csds/", body, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<StampedCfdi> StampAsync(ProviderAccount account, CfdiRequest r, CancellationToken ct)
    {
        var body = new
        {
            NameId = "1", // Factura
            CfdiType = "I",
            Serie = string.IsNullOrWhiteSpace(r.Serie) ? null : r.Serie,
            Folio = r.Folio,
            Currency = "MXN",
            ExpeditionPlace = r.Issuer.ZipCode,
            Exportation = "01",
            PaymentForm = r.PaymentForm,
            PaymentMethod = "PUE",
            Issuer = new { Rfc = r.Issuer.Rfc, Name = r.Issuer.Name, FiscalRegime = r.Issuer.TaxRegime },
            Receiver = new
            {
                Rfc = r.Receiver.Rfc, Name = r.Receiver.Name, CfdiUse = r.CfdiUse,
                FiscalRegime = r.Receiver.TaxRegime, TaxZipCode = r.Receiver.ZipCode,
            },
            Items = r.Concepts.Select(c => new
            {
                ProductCode = c.ProductCode,
                IdentificationNumber = c.Sku,
                Description = c.Description,
                Unit = c.UnitName,
                UnitCode = c.UnitCode,
                UnitPrice = c.UnitValue,
                Quantity = c.Quantity,
                Subtotal = c.Amount,
                Discount = c.Discount > 0 ? c.Discount : (decimal?)null,
                TaxObject = "02",
                Taxes = new[]
                {
                    new { Name = "IVA", Rate = c.TaxRate, Base = c.TaxBase, Total = c.Tax, IsRetention = false, IsFederalTax = true },
                },
                Total = c.Total,
            }).ToList(),
        };

        using var response = await SendAsync(account, HttpMethod.Post, "api-lite/3/cfdis", body, ct);
        await EnsureSuccessAsync(response, ct);
        var cfdi = await response.Content.ReadFromJsonAsync<CfdiResponse>(Json, ct)
            ?? throw new InvoicingException("Facturama no devolvió la factura timbrada.");
        var uuid = cfdi.Complement?.TaxStamp?.Uuid;
        if (string.IsNullOrEmpty(cfdi.Id) || string.IsNullOrEmpty(uuid))
            throw new InvoicingException("Facturama no devolvió el folio fiscal (UUID) de la factura.");
        return new StampedCfdi(cfdi.Id, uuid, cfdi.Total);
    }

    public async Task<byte[]> DownloadAsync(ProviderAccount account, string providerId, CfdiFileFormat format, CancellationToken ct)
    {
        var kind = format == CfdiFileFormat.Pdf ? "pdf" : "xml";
        using var response = await SendAsync(account, HttpMethod.Get, $"cfdi/{kind}/issuedLite/{Uri.EscapeDataString(providerId)}", null, ct);
        await EnsureSuccessAsync(response, ct);
        var file = await response.Content.ReadFromJsonAsync<FileResponse>(Json, ct);
        if (string.IsNullOrEmpty(file?.Content)) throw new InvoicingException("Facturama no devolvió el archivo de la factura.");
        return Convert.FromBase64String(file.Content);
    }

    public async Task<CancelOutcome> CancelAsync(ProviderAccount account, string providerId, string motive, string? replacementUuid, CancellationToken ct)
    {
        var url = $"api-lite/cfdis/{Uri.EscapeDataString(providerId)}?motive={Uri.EscapeDataString(motive)}" +
            $"&uuidReplacement={Uri.EscapeDataString(replacementUuid ?? string.Empty)}";
        using var response = await SendAsync(account, HttpMethod.Delete, url, null, ct);
        await EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<CancelResponse>(Json, ct);
        var status = result?.Status ?? "desconocido";
        // "canceled" is immediate; receivers who must accept leave it "pending" until they answer.
        var cancelled = status.StartsWith("cancel", StringComparison.OrdinalIgnoreCase);
        return new CancelOutcome(cancelled, status, result?.Message);
    }

    private async Task<HttpResponseMessage> SendAsync(ProviderAccount account, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, new Uri(new Uri(account.Sandbox ? SandboxUrl : ProductionUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{account.UserName}:{account.Password}")));
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
            throw new InvoicingException("Facturama rechazó el usuario o la contraseña. Revisa la cuenta en Configuración → Facturación y que tu suscripción esté activa.");
        if ((int)response.StatusCode >= 500)
            throw new InvoicingException("Facturama no está respondiendo en este momento. Intenta de nuevo en unos minutos.");

        var text = await response.Content.ReadAsStringAsync(ct);
        throw new InvoicingException(ErrorMessage(text) ?? $"Facturama rechazó la solicitud ({(int)response.StatusCode}).");
    }

    /// <summary>Facturama answers 400 with { Message, ModelState: { field: [errors] } }.</summary>
    public static string? ErrorMessage(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(body, Json);
            var details = error?.ModelState?.SelectMany(kv => kv.Value).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList() ?? [];
            if (details.Count > 0) return string.Join(" ", details);
            return string.IsNullOrWhiteSpace(error?.Message) ? null : error.Message;
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(body) || body.Length > 300 ? null : body;
        }
    }

    private sealed record CfdiResponse(string? Id, decimal Total, ComplementResponse? Complement);
    private sealed record ComplementResponse(TaxStampResponse? TaxStamp);
    private sealed record TaxStampResponse(string? Uuid);
    private sealed record FileResponse(string? Content);
    private sealed record CancelResponse(string? Status, string? Message);
    private sealed record ErrorResponse(string? Message, Dictionary<string, string[]>? ModelState);
}
