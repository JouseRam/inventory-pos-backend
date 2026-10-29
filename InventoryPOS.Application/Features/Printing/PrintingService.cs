using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Printing;

/// <summary>Builds tickets from sales and settings and hands them to the receipt printer.</summary>
public class PrintingService(IUnitOfWork uow, SettingService settings, ITicketPrinter printer, IIdentityService identity, IDateTimeProvider clock)
{
    public async Task<PrinterConfig> GetConfigAsync(CancellationToken ct = default)
    {
        var all = (await settings.ListAsync(ct)).ToDictionary(s => s.Key, s => s.Value);
        string? Get(string key) => all.GetValueOrDefault(key);
        int Int(string key, int fallback) => int.TryParse(Get(key), out var v) ? v : fallback;
        bool Bool(string key, bool fallback) => bool.TryParse(Get(key), out var v) ? v : fallback;

        var mode = PrinterModes.All.Contains(Get(SettingKeys.PrinterMode)) ? Get(SettingKeys.PrinterMode)! : PrinterModes.None;
        return new PrinterConfig(mode, Get(SettingKeys.PrinterName), Get(SettingKeys.PrinterHost), Int(SettingKeys.PrinterPort, 9100),
            Int(SettingKeys.PrinterPaperWidth, 80), Int(SettingKeys.PrinterCodePage, 858),
            Bool(SettingKeys.PrinterAutoPrint, true), Bool(SettingKeys.PrinterOpenDrawerOnCash, true));
    }

    public async Task<PrinterStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var c = await GetConfigAsync(ct);
        return new PrinterStatusDto(c.Enabled, c.Enabled && c.AutoPrint, c.Mode,
            c.Mode == PrinterModes.Network ? $"{c.Host}:{c.Port}" : c.Mode == PrinterModes.Windows ? c.PrinterName : null);
    }

    public IReadOnlyList<string> ListPrinters() => printer.ListInstalledPrinters();

    /// <summary>Prints a sale's ticket; opens the drawer when it was paid partly in cash and the setting asks for it.</summary>
    public async Task PrintSaleAsync(int saleId, bool reprint, CancellationToken ct = default)
    {
        var config = await RequireEnabledAsync(ct);
        var sale = await uow.Sales.GetWithDetailsAsync(saleId, ct) ?? throw NotFoundException.For("Venta", saleId);
        var cashier = sale.UserId == null ? null : await identity.GetDisplayNameAsync(sale.UserId, ct);

        var ticket = new TicketDocument(
            await HeaderAsync(ct), reprint ? "REIMPRESIÓN" : "TICKET DE VENTA", sale.Folio, clock.ToLocal(sale.CreatedAt), cashier,
            sale.Customer?.Name ?? "Mostrador",
            sale.Items.Select(i => new TicketLine(i.Description, i.Quantity, i.UnitPrice, i.Discount, i.Subtotal)).ToList(),
            sale.Subtotal, sale.Discount, sale.Total, sale.Tax,
            sale.Payments.Select(p => new TicketPayment(p.Method, p.Amount, p.Reference)).ToList(),
            sale.AmountPaid, sale.Change, sale.Notes, await settings.GetAsync(SettingKeys.TicketFooter, ct),
            sale.Status == SaleStatus.Cancelled);

        var openDrawer = !reprint && config.OpenDrawerOnCash && sale.Payments.Any(p => p.Method == PaymentMethods.Cash);
        await printer.PrintAsync(ticket, config, openDrawer, ct);
    }

    public async Task PrintTestAsync(CancellationToken ct = default)
    {
        var config = await RequireEnabledAsync(ct);
        var ticket = new TicketDocument(
            await HeaderAsync(ct), "PRUEBA DE IMPRESIÓN", "PRUEBA", clock.ToLocal(clock.UtcNow), "Configuración", "Acentos: áéíóú ñ Ñ ¡¿",
            [new TicketLine("Tinte 7.1 Rubio medio ceniza - 60 ml", 2, 189m, 0, 378m), new TicketLine("Oxidante 20 vol - 90 ml", 1, 45.5m, 5m, 40.5m)],
            418.5m, 0, 418.5m, 57.72m, [new TicketPayment(PaymentMethods.Cash, 418.5m, null)], 500m, 81.5m,
            $"Papel {config.PaperWidthMm} mm, {config.Columns} columnas", await settings.GetAsync(SettingKeys.TicketFooter, ct), false);
        await printer.PrintAsync(ticket, config, openDrawer: false, ct);
    }

    public async Task OpenDrawerAsync(CancellationToken ct = default) =>
        await printer.OpenDrawerAsync(await RequireEnabledAsync(ct), ct);

    private async Task<PrinterConfig> RequireEnabledAsync(CancellationToken ct)
    {
        var config = await GetConfigAsync(ct);
        if (!config.Enabled) throw new DomainException("No hay impresora configurada. Configúrala en Configuración › Impresora.");
        return config;
    }

    private async Task<IReadOnlyList<string>> HeaderAsync(CancellationToken ct)
    {
        var lines = new List<string>();
        foreach (var key in new[] { SettingKeys.BusinessName, SettingKeys.BusinessAddress, SettingKeys.BusinessPhone })
        {
            var value = await settings.GetAsync(key, ct);
            if (!string.IsNullOrWhiteSpace(value))
                lines.AddRange(value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        var rfc = await settings.GetAsync(SettingKeys.BusinessRfc, ct);
        if (!string.IsNullOrWhiteSpace(rfc)) lines.Add($"RFC {rfc.Trim()}");
        return lines;
    }
}
