using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using InventoryPOS.Application.Features.Printing;
using InventoryPOS.Domain.Common;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Infrastructure.Printing;

/// <summary>Sends ESC/POS bytes to a Windows printer queue (RAW) or straight to a network printer's TCP port.</summary>
public class EscPosTicketPrinter(ILogger<EscPosTicketPrinter> logger) : ITicketPrinter
{
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(5);

    public Task PrintAsync(TicketDocument ticket, PrinterConfig config, bool openDrawer, CancellationToken ct = default) =>
        SendAsync(EscPosTicketRenderer.Render(ticket, config, openDrawer), config, $"Ticket {ticket.Folio}", ct);

    public Task OpenDrawerAsync(PrinterConfig config, CancellationToken ct = default) =>
        SendAsync(EscPosTicketRenderer.DrawerKick, config, "Abrir cajón", ct);

    public IReadOnlyList<string> ListInstalledPrinters() =>
        OperatingSystem.IsWindows() ? WindowsSpooler.ListPrinters() : [];

    private async Task SendAsync(byte[] data, PrinterConfig config, string jobName, CancellationToken ct)
    {
        try
        {
            switch (config.Mode)
            {
                case PrinterModes.Network:
                    await SendToNetworkAsync(data, config, ct);
                    break;
                case PrinterModes.Windows when OperatingSystem.IsWindows():
                    if (string.IsNullOrWhiteSpace(config.PrinterName))
                        throw new DomainException("Elige la impresora de Windows en Configuración › Impresora.");
                    WindowsSpooler.SendRaw(config.PrinterName, jobName, data);
                    break;
                default:
                    throw new DomainException("No hay impresora configurada.");
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or Win32Exception or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Printing '{Job}' failed", jobName);
            throw new DomainException($"La impresora no respondió ({ex.Message}). Revisa que esté encendida y conectada.");
        }
    }

    private static async Task SendToNetworkAsync(byte[] data, PrinterConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new DomainException("Captura la IP de la impresora de red en Configuración › Impresora.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(NetworkTimeout);
        using var client = new TcpClient();
        await client.ConnectAsync(config.Host.Trim(), config.Port, timeout.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, timeout.Token);
        await stream.FlushAsync(timeout.Token);
    }

    /// <summary>winspool.drv: the documented way to send raw printer language bytes past the Windows driver.</summary>
    [SupportedOSPlatform("windows")]
    private static class WindowsSpooler
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DocInfo1
        {
            public string pDocName;
            public string? pOutputFile;
            public string pDatatype;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PrinterInfo4
        {
            public string pPrinterName;
            public string? pServerName;
            public uint Attributes;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo1 pDocInfo);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr pPrinterEnum, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

        private const uint PrinterEnumLocal = 0x2, PrinterEnumConnections = 0x4;

        public static void SendRaw(string printerName, string jobName, byte[] data)
        {
            if (!OpenPrinter(printerName, out var handle, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var doc = new DocInfo1 { pDocName = jobName, pDatatype = "RAW" };
                if (StartDocPrinter(handle, 1, ref doc) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    if (!StartPagePrinter(handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (!WritePrinter(handle, data, data.Length, out var written) || written != data.Length)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    EndPagePrinter(handle);
                }
                finally { EndDocPrinter(handle); }
            }
            finally { ClosePrinter(handle); }
        }

        public static IReadOnlyList<string> ListPrinters()
        {
            const uint flags = PrinterEnumLocal | PrinterEnumConnections;
            EnumPrinters(flags, null, 4, IntPtr.Zero, 0, out var needed, out _);
            if (needed == 0) return [];
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!EnumPrinters(flags, null, 4, buffer, needed, out _, out var count)) return [];
                var size = Marshal.SizeOf<PrinterInfo4>();
                return Enumerable.Range(0, (int)count)
                    .Select(i => Marshal.PtrToStructure<PrinterInfo4>(buffer + i * size).pPrinterName)
                    .OrderBy(n => n).ToList();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
}
