using System.Management;
using InventoryPOS.Licensing;
using Microsoft.Win32;

namespace InventoryPOS.Desktop;

/// <summary>Reads this computer's traits for the machine code: Windows installation id, board UUID and board (or BIOS) serial.</summary>
internal static class MachineFingerprint
{
    public static MachineCode Current() => MachineCode.FromTraits(
        Safe(WindowsId),
        Safe(() => Wmi("Win32_ComputerSystemProduct", "UUID")),
        Safe(() => Wmi("Win32_BaseBoard", "SerialNumber") ?? Wmi("Win32_BIOS", "SerialNumber")));

    private static string? WindowsId()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid") as string;
    }

    private static string? Wmi(string table, string property)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {table}");
        using var results = searcher.Get();
        foreach (var item in results)
        {
            using (item)
            {
                var value = item[property]?.ToString();
                if (MachineCode.IsUseful(value)) return value;
            }
        }
        return null;
    }

    /// <summary>A trait that cannot be read counts as unknown; the other two still identify the computer.</summary>
    private static string? Safe(Func<string?> read)
    {
        try { return read(); }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException
                                      or System.Security.SecurityException or IOException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
