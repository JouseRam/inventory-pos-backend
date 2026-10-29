using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace InventoryPOS.Desktop;

/// <summary>The register window: an embedded Edge (WebView2) showing the local app. F11 toggles full screen.</summary>
public sealed class MainForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Uri _home;
    private readonly string _profileFolder;
    private FormWindowState _previousState;

    public MainForm(Uri home, string profileFolder)
    {
        _home = home;
        _profileFolder = profileFolder;
        Text = "Matiz POS";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        MinimumSize = new Size(1024, 680);
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        Controls.Add(_web);
        Load += async (_, _) => await InitializeWebViewAsync();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F11) ToggleFullScreen(); };
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profileFolder);
            await _web.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                "Falta el componente Microsoft Edge WebView2 Runtime.\n\nDescárgalo de https://go.microsoft.com/fwlink/p/?LinkId=2124703, instálalo y vuelve a abrir Matiz POS.",
                "Matiz POS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = Debugger.IsAttached;
        core.Settings.AreDefaultContextMenusEnabled = Debugger.IsAttached;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = Debugger.IsAttached;
        core.DocumentTitleChanged += (_, _) => Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Matiz POS" : core.DocumentTitle;

        // Keep the register on the local app; external links open in the default browser.
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith(_home.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                OpenExternal(e.Uri);
            }
        };
        core.NewWindowRequested += (_, e) => { e.Handled = true; OpenExternal(e.Uri); };
        // The page handles F2/F9; F11 must still reach the window when the page has focus.
        _web.KeyDown += (_, e) => { if (e.KeyCode == Keys.F11) ToggleFullScreen(); };

        core.Navigate(_home.ToString());
    }

    private void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = _previousState;
        }
        else
        {
            _previousState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
        }
    }

    private static void OpenExternal(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            Process.Start(new ProcessStartInfo(u.ToString()) { UseShellExecute = true });
    }
}
