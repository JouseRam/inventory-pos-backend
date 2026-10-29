using System.Globalization;
using System.Text;
using InventoryPOS.Application.Features.Printing;

namespace InventoryPOS.Infrastructure.Printing;

/// <summary>
/// Turns a <see cref="TicketDocument"/> into ESC/POS bytes for 58 mm (32 columns) or 80 mm (48 columns) paper.
/// Text is encoded in the configured code page (PC858 by default, which covers Spanish accents and ñ);
/// code page 0 strips accents for printers without international tables.
/// </summary>
public static class EscPosTicketRenderer
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    static EscPosTicketRenderer() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // ESC/POS commands
    private static readonly byte[] Init = [0x1B, 0x40];
    private static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];
    private static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];
    private static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] DoubleSize = [0x1D, 0x21, 0x11];
    private static readonly byte[] DoubleHeight = [0x1D, 0x21, 0x01];
    private static readonly byte[] NormalSize = [0x1D, 0x21, 0x00];
    private static readonly byte[] FeedAndCut = [0x1B, 0x64, 0x04, 0x1D, 0x56, 0x42, 0x00];

    /// <summary>Pulse on drawer pin 2 (ESC p 0 25 250), the wiring most cash drawers use.</summary>
    public static readonly byte[] DrawerKick = [0x1B, 0x70, 0x00, 0x19, 0xFA];

    public static byte[] Render(TicketDocument t, PrinterConfig config, bool openDrawer)
    {
        var w = new Writer(config);
        var cols = config.Columns;
        w.Raw(Init).CodePage();
        if (openDrawer) w.Raw(DrawerKick);

        w.Raw(AlignCenter);
        if (t.HeaderLines.Count > 0)
        {
            w.Raw(BoldOn, DoubleHeight).Line(t.HeaderLines[0]).Raw(NormalSize, BoldOff);
            foreach (var line in t.HeaderLines.Skip(1)) w.Wrapped(line, cols);
        }
        w.Feed().Raw(BoldOn).Line(t.Title).Raw(BoldOff);
        if (t.Cancelled) w.Raw(BoldOn).Line("*** VENTA CANCELADA ***").Raw(BoldOff);

        w.Raw(AlignLeft).Rule(cols);
        w.Line(Pair($"Folio: {t.Folio}", t.LocalDate.ToString("dd/MM/yyyy HH:mm", Mx), cols));
        if (!string.IsNullOrWhiteSpace(t.Cashier)) w.Line(Fit($"Atendió: {t.Cashier}", cols));
        w.Line(Fit($"Cliente: {t.Customer}", cols)).Rule(cols);

        foreach (var item in t.Lines)
        {
            w.Wrapped(item.Description, cols);
            var detail = $"  {Qty(item.Quantity)} x {Money(item.UnitPrice)}" + (item.Discount > 0 ? $" -{Money(item.Discount)}" : "");
            w.Line(Pair(detail, Money(item.Total), cols));
        }
        w.Rule(cols);

        w.Line(Pair("Subtotal", Money(t.Subtotal), cols));
        if (t.Discount > 0) w.Line(Pair("Descuento", "-" + Money(t.Discount), cols));
        w.Raw(BoldOn, DoubleHeight).Line(Pair("TOTAL", Money(t.Total), cols)).Raw(NormalSize, BoldOff);
        if (t.Tax > 0) w.Line(Pair("IVA incluido", Money(t.Tax), cols));
        w.Feed();
        foreach (var p in t.Payments)
            w.Line(Pair(p.Reference == null ? p.Method : $"{p.Method} ({p.Reference})", Money(p.Amount), cols));
        if (t.AmountPaid > t.Total || t.Change > 0)
        {
            w.Line(Pair("Recibido", Money(t.AmountPaid), cols));
            w.Raw(BoldOn).Line(Pair("Cambio", Money(t.Change), cols)).Raw(BoldOff);
        }
        if (!string.IsNullOrWhiteSpace(t.Notes)) w.Feed().Wrapped(t.Notes, cols);

        if (!string.IsNullOrWhiteSpace(t.Footer))
        {
            w.Rule(cols).Raw(AlignCenter);
            foreach (var line in t.Footer.Split('\n', StringSplitOptions.TrimEntries)) w.Wrapped(line, cols);
        }
        w.Raw(AlignLeft, FeedAndCut);
        return w.ToArray();
    }

    public static string Money(decimal value) => value.ToString("C2", Mx);
    private static string Qty(decimal value) => value.ToString("0.###", Mx);

    /// <summary>Left text and right-aligned value on one line; the left side is cut to make room.</summary>
    public static string Pair(string left, string right, int cols)
    {
        var room = Math.Max(1, cols - right.Length - 1);
        if (left.Length > room) left = left[..room];
        return left + new string(' ', cols - left.Length - right.Length) + right;
    }

    private static string Fit(string text, int cols) => text.Length <= cols ? text : text[..cols];

    /// <summary>Word-wraps to the paper width so long product names are not cut mid-word.</summary>
    public static IEnumerable<string> Wrap(string text, int cols)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > cols) { if (line.Length > 0) { yield return line.ToString(); line.Clear(); } yield return w[..cols]; w = w[cols..]; }
            if (line.Length > 0 && line.Length + 1 + w.Length > cols) { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private sealed class Writer(PrinterConfig config)
    {
        private readonly MemoryStream _buffer = new();
        private readonly Encoding _encoding = config.CodePage > 0 ? Encoding.GetEncoding(config.CodePage) : Encoding.ASCII;

        public Writer Raw(params byte[][] commands)
        {
            foreach (var c in commands) _buffer.Write(c);
            return this;
        }

        /// <summary>Selects the character table matching the encoding (ESC t n).</summary>
        public Writer CodePage()
        {
            byte? table = config.CodePage switch { 858 => 19, 850 => 2, 437 => 0, 1252 => 16, _ => null };
            return table.HasValue ? Raw([0x1B, 0x74, table.Value]) : this;
        }

        public Writer Line(string text)
        {
            _buffer.Write(_encoding.GetBytes(config.CodePage > 0 ? text : StripAccents(text)));
            _buffer.WriteByte(0x0A);
            return this;
        }

        public Writer Wrapped(string text, int cols)
        {
            foreach (var line in Wrap(text, cols)) Line(line);
            return this;
        }

        public Writer Rule(int cols) => Line(new string('-', cols));
        public Writer Feed() => Line(string.Empty);
        public byte[] ToArray() => _buffer.ToArray();

        private static string StripAccents(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString().Replace('¡', '!').Replace('¿', '?');
        }
    }
}
