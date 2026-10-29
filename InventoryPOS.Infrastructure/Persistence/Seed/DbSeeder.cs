using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seed run at startup: only inserts what is missing, so settings or catalogs
/// edited by the store are never overwritten.
/// </summary>
public class DbSeeder(ApplicationDbContext db, UserManager<AppUser> users, IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedFolioSequencesAsync(ct);
        await SeedSettingsAsync(ct);
        await SeedCategoriesAsync(ct);
        await db.SaveChangesAsync(ct);
        await SeedAdminAsync();
    }

    private async Task SeedFolioSequencesAsync(CancellationToken ct)
    {
        await EnsureSequence(FolioSeries.Sale, "V-", ct);
        await EnsureSequence(FolioSeries.Purchase, "REC-", ct);
        await EnsureSequence(FolioSeries.CashSession, "CJ-", ct);
    }

    private async Task EnsureSequence(string name, string prefix, CancellationToken ct)
    {
        if (!await db.FolioSequences.AnyAsync(f => f.Name == name, ct))
            db.FolioSequences.Add(new FolioSequence { Name = name, Prefix = prefix, NextValue = 1 });
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var existing = (await db.Settings.Select(s => s.Key).ToListAsync(ct)).ToHashSet();
        void EnsureSetting(string key, string label, string type, string group, string? value)
        {
            if (existing.Add(key))
                db.Settings.Add(new Setting { Key = key, Label = label, Type = type, Group = group, Value = value });
        }

        EnsureSetting(SettingKeys.BusinessName, "Nombre del negocio", "text", "Negocio", "Mi Tienda de Belleza");
        EnsureSetting(SettingKeys.BusinessRfc, "RFC", "text", "Negocio", null);
        EnsureSetting(SettingKeys.BusinessAddress, "Dirección", "textarea", "Negocio", null);
        EnsureSetting(SettingKeys.BusinessPhone, "Teléfono", "text", "Negocio", null);
        EnsureSetting(SettingKeys.TaxRate, "IVA incluido en precios (%)", "number", "Ventas", "16");
        EnsureSetting(SettingKeys.TicketFooter, "Pie del ticket", "textarea", "Ventas", "¡Gracias por su compra!");
        EnsureSetting(SettingKeys.ExpirationWarningDays, "Avisar caducidad con anticipación (días)", "number", "Inventario", "60");
        EnsureSetting(SettingKeys.PrinterMode, "Conexión de la impresora", "text", "Impresora", "none");
        EnsureSetting(SettingKeys.PrinterName, "Impresora de Windows", "text", "Impresora", null);
        EnsureSetting(SettingKeys.PrinterHost, "IP de la impresora de red", "text", "Impresora", null);
        EnsureSetting(SettingKeys.PrinterPort, "Puerto de la impresora de red", "number", "Impresora", "9100");
        EnsureSetting(SettingKeys.PrinterPaperWidth, "Ancho del papel (mm)", "number", "Impresora", "80");
        EnsureSetting(SettingKeys.PrinterCodePage, "Página de códigos", "number", "Impresora", "858");
        EnsureSetting(SettingKeys.PrinterAutoPrint, "Imprimir al cobrar", "bool", "Impresora", "true");
        EnsureSetting(SettingKeys.PrinterOpenDrawerOnCash, "Abrir cajón en cobros con efectivo", "bool", "Impresora", "true");
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        var tree = new Dictionary<string, string[]>
        {
            ["Cabello"] = ["Tintes", "Decolorantes y oxidantes", "Tratamientos", "Shampoo y acondicionador", "Styling"],
            ["Uñas"] = ["Esmaltes", "Gel y acrílico"],
            ["Maquillaje"] = [],
            ["Cuidado de la piel"] = [],
            ["Herramientas y accesorios"] = ["Secadoras y planchas", "Cepillos y peines"],
        };
        if (await db.Categories.AnyAsync(ct)) return;

        foreach (var (parent, children) in tree)
        {
            var category = new Category { Name = parent };
            foreach (var child in children) category.Children.Add(new Category { Name = child });
            db.Categories.Add(category);
        }
    }

    /// <summary>
    /// Creates the first super admin from configuration (Seed:AdminUserName / Seed:AdminPassword) when no user exists,
    /// as used by development; installed registers create it from the setup screen instead.
    /// </summary>
    private async Task SeedAdminAsync()
    {
        if (await users.Users.AnyAsync()) return;

        var userName = config["Seed:AdminUserName"] ?? "admin";
        var password = config["Seed:AdminPassword"];
        // Without a configured password the first admin is created from the setup screen.
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("No users yet; waiting for the first-run setup screen.");
            return;
        }

        var admin = new AppUser { UserName = userName, FullName = config["Seed:AdminFullName"] ?? "Administrador", IsSuperAdmin = true };
        var result = await users.CreateAsync(admin, password);
        if (result.Succeeded)
            logger.LogInformation("Created initial super admin '{UserName}'.", userName);
        else
            logger.LogError("Could not create initial admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
