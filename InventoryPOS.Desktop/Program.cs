using System.Net;
using System.Net.Sockets;
using InventoryPOS.API;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Desktop;

/// <summary>
/// Desktop register: starts the POS web app in-process on the loopback address over a local SQLite
/// file, then shows it in a WebView2 window. Everything works without internet.
/// </summary>
internal static class Program
{
    private const string SettingsFile = "desktopsettings.json";

    [STAThread]
    private static void Main(string[] args)
    {
        // Two copies would run two servers over the same database file.
        using var singleInstance = new Mutex(true, @"Local\MatizPOS.Register", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Matiz POS ya está abierto en esta computadora.", "Matiz POS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        var baseConfig = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(SettingsFile, optional: true)
            .Build();
        var paths = DesktopPaths.Resolve(baseConfig);
        var logs = new FileLoggerProvider(paths.Logs);
        logs.PruneOldFiles();
        var port = PickPort(baseConfig.GetValue("Desktop:PreferredPort", 51080));

        WebApplication app;
        try
        {
            app = PosWebApp.Build(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                EnvironmentName = "Production",
            }, builder =>
            {
                builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, SettingsFile), optional: true);
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [PosWebApp.DesktopFlag] = "true",
                    ["Pos:Role"] = "Register",
                    ["Database:Provider"] = "Sqlite",
                    ["Database:AutoMigrate"] = "true",
                    ["Database:BackupDirectory"] = paths.Backups,
                    ["ConnectionStrings:Sqlite"] = $"Data Source={paths.Database}",
                    ["Jwt:Key"] = SigningKeyStore.GetOrCreate(paths.KeyFile),
                });
                builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(logs);
            });
            PosWebApp.InitializeAsync(app).GetAwaiter().GetResult();
            app.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logs.Write("Startup", LogLevel.Critical, "The register could not start", ex);
            MessageBox.Show($"No se pudo iniciar Matiz POS:\n\n{ex.Message}\n\nDetalle en {paths.Logs}", "Matiz POS",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            System.Windows.Forms.Application.Run(new MainForm(new Uri($"http://127.0.0.1:{port}/"), paths.WebView));
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            app.StopAsync(stopTimeout.Token).GetAwaiter().GetResult();
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Uses the preferred port when free so the window keeps the same origin (and its saved session);
    /// falls back to any free port.
    /// </summary>
    private static int PickPort(int preferred)
    {
        foreach (var candidate in new[] { preferred, 0 })
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, candidate);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch (SocketException) when (candidate != 0) { }
        }
        throw new InvalidOperationException("No free local port.");
    }
}
