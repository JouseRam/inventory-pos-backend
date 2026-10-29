using System.Text;
using InventoryPOS.Application.Features.Printing;
using InventoryPOS.Infrastructure.Printing;

namespace InventoryPOS.Application.Tests;

public class TicketRendererTests
{
    private static TicketDocument Ticket(string description = "Igora Royal - 7.1 Rubio medio ceniza - 60 ml") => new(
        ["Bella Tinte", "Av. Juárez 120, Centro"], "TICKET DE VENTA", "V-000123", new DateTime(2026, 10, 29, 18, 30, 0), "Ana López", "Mostrador",
        [new TicketLine(description, 2, 189m, 0, 378m)], 378m, 0, 378m, 52.14m,
        [new TicketPayment("Tarjeta", 100m, "1234"), new TicketPayment("Efectivo", 278m, null)], 600m, 222m, null, "¡Gracias por su compra!", false);

    private static PrinterConfig Config(int width = 80, int codePage = 858) =>
        new(PrinterModes.Network, null, "127.0.0.1", 9100, width, codePage, true, true);

    [Theory]
    [InlineData(80, 48)]
    [InlineData(58, 32)]
    public void No_line_exceeds_the_paper_width(int paper, int cols)
    {
        var description = "Tratamiento reconstructor de keratina con aceite de argán para cabello maltratado 500 ml";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = EscPosTicketRenderer.Render(Ticket(description), Config(paper), openDrawer: false);
        var text = Encoding.GetEncoding(858).GetString(bytes);

        Assert.Contains("$378.00", text);
        foreach (var line in EscPosTicketRenderer.Wrap(description, cols)) Assert.True(line.Length <= cols);
        Assert.Equal(cols, EscPosTicketRenderer.Pair("TOTAL", "$378.00", cols).Length);
    }

    [Fact]
    public void Accents_are_encoded_in_pc858_and_the_table_is_selected()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = EscPosTicketRenderer.Render(Ticket(), Config(), openDrawer: false);
        var selectPc858 = new byte[] { 0x1B, 0x74, 19 };

        Assert.True(bytes.AsSpan().IndexOf(selectPc858) >= 0);
        Assert.Contains("Juárez", Encoding.GetEncoding(858).GetString(bytes));
        Assert.Contains("Atendió: Ana López", Encoding.GetEncoding(858).GetString(bytes));
    }

    [Fact]
    public void Code_page_zero_strips_accents()
    {
        var bytes = EscPosTicketRenderer.Render(Ticket(), Config(codePage: 0), openDrawer: false);
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("Juarez", text);
        Assert.Contains("!Gracias", text);
    }

    [Fact]
    public void Drawer_pulse_is_sent_only_when_requested_and_paper_is_cut()
    {
        var with = EscPosTicketRenderer.Render(Ticket(), Config(), openDrawer: true);
        var without = EscPosTicketRenderer.Render(Ticket(), Config(), openDrawer: false);
        var cut = new byte[] { 0x1D, 0x56, 0x42, 0x00 };

        Assert.True(with.AsSpan().IndexOf(EscPosTicketRenderer.DrawerKick) >= 0);
        Assert.True(without.AsSpan().IndexOf(EscPosTicketRenderer.DrawerKick) < 0);
        Assert.True(without.AsSpan().IndexOf(cut) > 0);
    }
}
