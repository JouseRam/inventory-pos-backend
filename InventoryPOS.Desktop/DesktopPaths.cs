using Microsoft.Extensions.Configuration;

namespace InventoryPOS.Desktop;

/// <summary>Where the register keeps its data: database, backups, logs, browser profile and signing key.</summary>
public record DesktopPaths(string Root, string Database, string Backups, string Logs, string WebView, string KeyFile)
{
    public static DesktopPaths Resolve(IConfiguration config)
    {
        var root = Environment.GetEnvironmentVariable("MATIZPOS_DATA");
        if (string.IsNullOrWhiteSpace(root)) root = config["Desktop:DataDirectory"];
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MatizPOS");

        var paths = new DesktopPaths(
            root,
            Path.Combine(root, "data", "pos.db"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "webview"),
            Path.Combine(root, "data", "signing.key"));

        foreach (var dir in new[] { Path.GetDirectoryName(paths.Database)!, paths.Backups, paths.Logs, paths.WebView })
            Directory.CreateDirectory(dir);
        return paths;
    }
}
