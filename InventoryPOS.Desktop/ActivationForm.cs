using InventoryPOS.Licensing;

namespace InventoryPOS.Desktop;

/// <summary>First start on a computer: shows its code to send to the vendor and takes the activation key back.</summary>
internal sealed class ActivationForm : Form
{
    private readonly Func<string, LicenseCheck> _check;
    private readonly TextBox _key = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 90, Font = new Font("Consolas", 9f) };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(520, 0) };

    public string ActivatedKey { get; private set; } = "";

    public ActivationForm(MachineCode machine, string? problem, Func<string, LicenseCheck> check)
    {
        _check = check;
        Text = $"Activar {MainForm.StoreName}";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 400);
        Font = new Font("Segoe UI", 10f);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20), AutoScroll = true };
        Label Para(string text, bool bold = false) => new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 0, 0, 8),
            Font = bold ? new Font(Font, FontStyle.Bold) : Font,
        };

        layout.Controls.Add(Para("Esta computadora necesita activarse", bold: true));
        layout.Controls.Add(Para("Envía este código a tu proveedor del sistema. Te dará una clave de activación que solo sirve en esta computadora."));

        var code = new TextBox { Text = machine.ToString(), ReadOnly = true, Width = 400, Font = new Font("Consolas", 13f, FontStyle.Bold) };
        var copy = new Button { Text = "Copiar código", AutoSize = true };
        copy.Click += (_, _) => { Clipboard.SetText(code.Text); copy.Text = "Copiado ✓"; };
        var codeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 14) };
        codeRow.Controls.AddRange([code, copy]);
        layout.Controls.Add(codeRow);

        layout.Controls.Add(Para("Clave de activación"));
        _key.Width = 520;
        layout.Controls.Add(_key);
        var load = new Button { Text = "Cargar desde archivo…", AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
        load.Click += (_, _) => LoadFile();
        layout.Controls.Add(load);

        _error.Text = problem ?? "";
        layout.Controls.Add(_error);

        var activate = new Button { Text = "Activar", AutoSize = true, DialogResult = DialogResult.None };
        var exit = new Button { Text = "Salir", AutoSize = true, DialogResult = DialogResult.Cancel };
        activate.Click += (_, _) => TryActivate();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(16, 8, 16, 12) };
        buttons.Controls.AddRange([activate, exit]);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = activate;
        CancelButton = exit;
    }

    private void LoadFile()
    {
        using var dialog = new OpenFileDialog { Title = "Clave de activación", Filter = "Clave de activación (*.txt;*.key)|*.txt;*.key|Todos los archivos|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _key.Text = File.ReadAllText(dialog.FileName).Trim();
    }

    private void TryActivate()
    {
        var result = _check(_key.Text);
        if (!result.IsValid)
        {
            _error.Text = result.Message;
            return;
        }
        ActivatedKey = _key.Text.Trim();
        MessageBox.Show(this, $"Listo. {result.Message}", MainForm.StoreName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
    }
}
