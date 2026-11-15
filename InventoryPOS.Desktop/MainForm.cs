using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace InventoryPOS.Desktop;

/// <summary>The register window: an embedded Edge (WebView2) showing the local app. F11 toggles full screen.</summary>
public sealed class MainForm : Form
{
    /// <summary>The store's name, for the window and messages before the app has loaded its own.</summary>
    public const string StoreName = "Anna’s Beauty Supply";

    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    /// <summary>Tray icon used to show Windows notifications (stock at its minimum) sent by the page.</summary>
    private readonly NotifyIcon _tray = new() { Text = StoreName, Visible = false };
    private readonly Uri _home;
    private readonly string _profileFolder;
    private FormWindowState _previousState;

    public MainForm(Uri home, string profileFolder)
    {
        _home = home;
        _profileFolder = profileFolder;
        Text = StoreName;
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        _tray.Icon = Icon;
        _tray.BalloonTipClicked += (_, _) => ShowFromNotification();
        FormClosed += (_, _) => _tray.Dispose();
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
                "Falta el componente Microsoft Edge WebView2 Runtime.\n\nDescárgalo de https://go.microsoft.com/fwlink/p/?LinkId=2124703, instálalo y vuelve a abrir el punto de venta.",
                StoreName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = Debugger.IsAttached;
        core.Settings.AreDefaultContextMenusEnabled = Debugger.IsAttached;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = Debugger.IsAttached;
        core.DocumentTitleChanged += (_, _) => Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? StoreName : core.DocumentTitle;

        // Keep the register on the local app; external links open in the default browser.
        core.NavigationStarting += (_, e) =>
        {
            // blob: carries files the page builds itself (invoice PDF/XML downloads); it never leaves this machine.
            if (!e.Uri.StartsWith(_home.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) &&
                !e.Uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                OpenExternal(e.Uri);
            }
        };
        core.NewWindowRequested += (_, e) => { e.Handled = true; OpenExternal(e.Uri); };
        core.WebMessageReceived += (_, e) => OnPageMessage(e.WebMessageAsJson);
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

    /// <summary>Messages from the page. Only "notify" is known: a Windows notification, for example a product at its minimum.</summary>
    private void OnPageMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.GetProperty("type").GetString() != "notify") return;
            var title = Limit(root.TryGetProperty("title", out var t) ? t.GetString() : null, 63) ?? StoreName;
            var body = Limit(root.TryGetProperty("body", out var b) ? b.GetString() : null, 255) ?? "";
            _tray.Visible = true;
            _tray.ShowBalloonTip(10000, title, body.Length > 0 ? body : title, ToolTipIcon.Warning);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Not a message for us.
        }
    }

    private static string? Limit(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= max ? text : text[..(max - 1)] + "…";

    /// <summary>Clicking the notification brings the register forward on the suggested orders.</summary>
    private void ShowFromNotification()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Maximized;
        Activate();
        _web.CoreWebView2?.Navigate(new Uri(_home, "orders.html").ToString());
    }

    private static void OpenExternal(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            Process.Start(new ProcessStartInfo(u.ToString()) { UseShellExecute = true });
    }
}
