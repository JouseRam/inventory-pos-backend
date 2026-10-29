namespace InventoryPOS.Application.Features.Printing;

public static class PrinterModes
{
    public const string None = "none";
    /// <summary>A printer installed in Windows (USB thermal printers), fed raw ESC/POS bytes.</summary>
    public const string Windows = "windows";
    /// <summary>A network printer listening on a raw TCP port (usually 9100).</summary>
    public const string Network = "network";

    public static readonly string[] All = [None, Windows, Network];
}

public record PrinterConfig(
    string Mode, string? PrinterName, string? Host, int Port, int PaperWidthMm, int CodePage, bool AutoPrint, bool OpenDrawerOnCash)
{
    public bool Enabled => Mode is PrinterModes.Windows or PrinterModes.Network;
    /// <summary>Characters per line in the printer's standard font.</summary>
    public int Columns => PaperWidthMm <= 58 ? 32 : 48;
}

/// <summary>What the browser needs to know: whether to print after a sale.</summary>
public record PrinterStatusDto(bool Enabled, bool AutoPrint, string Mode, string? Target);

public record TicketLine(string Description, decimal Quantity, decimal UnitPrice, decimal Discount, decimal Total);
public record TicketPayment(string Method, decimal Amount, string? Reference);

/// <summary>Printer-agnostic content of a sales ticket.</summary>
public record TicketDocument(
    IReadOnlyList<string> HeaderLines, string Title, string Folio, DateTime LocalDate, string? Cashier, string Customer,
    IReadOnlyList<TicketLine> Lines, decimal Subtotal, decimal Discount, decimal Total, decimal Tax,
    IReadOnlyList<TicketPayment> Payments, decimal AmountPaid, decimal Change, string? Notes, string? Footer, bool Cancelled);

/// <summary>Sends documents to the configured receipt printer.</summary>
public interface ITicketPrinter
{
    Task PrintAsync(TicketDocument ticket, PrinterConfig config, bool openDrawer, CancellationToken ct = default);
    Task OpenDrawerAsync(PrinterConfig config, CancellationToken ct = default);
    IReadOnlyList<string> ListInstalledPrinters();
}
