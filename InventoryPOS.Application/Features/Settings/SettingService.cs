using System.Globalization;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Invoicing;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Settings;

public record SettingDto(string Key, string? Value, string Label, string Type, string Group);

/// <summary>Well-known setting keys; defaults are seeded by the infrastructure seeder.</summary>
public static class SettingKeys
{
    public const string BusinessName = "BusinessName";
    public const string BusinessRfc = "BusinessRfc";
    public const string BusinessAddress = "BusinessAddress";
    public const string BusinessPhone = "BusinessPhone";
    /// <summary>Razón social as registered with the SAT (issuer of the CFDI).</summary>
    public const string BusinessLegalName = "BusinessLegalName";
    /// <summary>c_RegimenFiscal code of the business.</summary>
    public const string BusinessTaxRegime = "BusinessTaxRegime";
    /// <summary>Código postal of the place of issue (LugarExpedicion).</summary>
    public const string BusinessZipCode = "BusinessZipCode";
    public const string TaxRate = "TaxRate";
    public const string TicketFooter = "TicketFooter";
    public const string ExpirationWarningDays = "ExpirationWarningDays";
    public const string PrinterMode = "PrinterMode";
    public const string PrinterName = "PrinterName";
    public const string PrinterHost = "PrinterHost";
    public const string PrinterPort = "PrinterPort";
    public const string PrinterPaperWidth = "PrinterPaperWidth";
    public const string PrinterCodePage = "PrinterCodePage";
    public const string PrinterAutoPrint = "PrinterAutoPrint";
    public const string PrinterOpenDrawerOnCash = "PrinterOpenDrawerOnCash";
}

public class SettingService(IUnitOfWork uow, PosGuard guard)
{
    /// <summary>Settings of this machine (its printer); never synced and editable on a paired register.</summary>
    public const string LocalGroup = "Impresora";

    private IRepository<Setting> Repo => uow.Repository<Setting>();

    public async Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken ct = default) =>
        (await Repo.ListAsync(null, ct)).OrderBy(s => s.Group).ThenBy(s => s.Id)
            .Select(s => new SettingDto(s.Key, s.Value, s.Label, s.Type, s.Group)).ToList();

    /// <summary>Updates existing keys only; unknown keys are rejected so typos do not create settings.</summary>
    public async Task<IReadOnlyList<SettingDto>> UpdateAsync(IDictionary<string, string?> values, CancellationToken ct = default)
    {
        var settings = (await Repo.ListAsync(null, ct)).ToDictionary(s => s.Key);
        if (values.Keys.Any(k => settings.TryGetValue(k, out var s) && s.Group != LocalGroup))
            await guard.EnsureCanAdministerAsync(ct);
        foreach (var (key, value) in values)
        {
            if (!settings.TryGetValue(key, out var setting))
                throw new NotFoundException($"La configuración '{key}' no existe.");
            if (setting.Type == "number" && !string.IsNullOrWhiteSpace(value) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                throw new Domain.Common.DomainException($"'{setting.Label}' debe ser numérico.");
            setting.Value = FiscalValue(key, value) ?? value?.Trim();
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await uow.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    /// <summary>Validates and normalizes the fiscal settings; null for any other key.</summary>
    private static string? FiscalValue(string key, string? value)
    {
        var normalized = FiscalRules.Normalize(value);
        if (normalized == null) return null;
        switch (key)
        {
            case SettingKeys.BusinessRfc when !FiscalRules.IsValidRfc(normalized):
                throw new Domain.Common.DomainException("RFC del negocio inválido: 12 caracteres para persona moral o 13 para persona física.");
            case SettingKeys.BusinessTaxRegime when !FiscalRules.IsValidTaxRegime(normalized):
                throw new Domain.Common.DomainException("Elige un régimen fiscal del catálogo del SAT.");
            case SettingKeys.BusinessZipCode when !FiscalRules.IsValidZip(normalized):
                throw new Domain.Common.DomainException("El código postal de expedición debe tener 5 dígitos.");
            case SettingKeys.BusinessRfc or SettingKeys.BusinessLegalName or SettingKeys.BusinessTaxRegime or SettingKeys.BusinessZipCode:
                return normalized;
            default:
                return null;
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        (await Repo.FirstOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
        decimal.TryParse(await GetAsync(key, ct), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
