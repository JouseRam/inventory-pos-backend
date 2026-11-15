using InventoryPOS.Licensing;

namespace InventoryPOS.Desktop;

/// <summary>
/// The register opens only on the computer it was activated for. The activation key is kept next to the data; copying the
/// program or its data folder to another computer asks for a new key, which only the vendor can make.
/// </summary>
internal static class LicenseGate
{
    /// <summary>True when the register may open: already activated here, or activated now by the person at the register.</summary>
    public static bool Ensure(DesktopPaths paths)
    {
#if DEBUG
        // Running from Visual Studio does not ask for activation, unless testing it on purpose.
        if (Environment.GetEnvironmentVariable("MATIZPOS_LICENSE") != "1") return true;
#endif
        if (string.IsNullOrWhiteSpace(LicensePublicKey.Value))
        {
            MessageBox.Show("Esta copia del programa no tiene configurada su llave de licencias.\n\nComunícate con tu proveedor del sistema.",
                MainForm.StoreName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        MachineCode machine;
        try { machine = MachineFingerprint.Current(); }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message + "\n\nComunícate con tu proveedor del sistema.", MainForm.StoreName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        var file = Path.Combine(paths.Root, "licencia.key");
        var saved = File.Exists(file) ? File.ReadAllText(file) : null;
        var check = LicenseKey.Check(saved, LicensePublicKey.Value, machine, DateOnly.FromDateTime(DateTime.Today));
        if (check.IsValid) return true;

        using var form = new ActivationForm(machine, check.Status == LicenseStatus.Missing ? null : check.Message,
            key => LicenseKey.Check(key, LicensePublicKey.Value, machine, DateOnly.FromDateTime(DateTime.Today)));
        if (form.ShowDialog() != DialogResult.OK) return false;
        File.WriteAllText(file, form.ActivatedKey + Environment.NewLine);
        return true;
    }
}
