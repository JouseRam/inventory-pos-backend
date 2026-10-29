using System.Globalization;
using InventoryPOS.Application.Common;
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

public class SettingService(IUnitOfWork uow)
{
    private IRepository<Setting> Repo => uow.Repository<Setting>();

    public async Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken ct = default) =>
        (await Repo.ListAsync(null, ct)).OrderBy(s => s.Group).ThenBy(s => s.Id)
            .Select(s => new SettingDto(s.Key, s.Value, s.Label, s.Type, s.Group)).ToList();

    /// <summary>Updates existing keys only; unknown keys are rejected so typos do not create settings.</summary>
    public async Task<IReadOnlyList<SettingDto>> UpdateAsync(IDictionary<string, string?> values, CancellationToken ct = default)
    {
        var settings = (await Repo.ListAsync(null, ct)).ToDictionary(s => s.Key);
        foreach (var (key, value) in values)
        {
            if (!settings.TryGetValue(key, out var setting))
                throw new NotFoundException($"La configuración '{key}' no existe.");
            if (setting.Type == "number" && !string.IsNullOrWhiteSpace(value) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                throw new Domain.Common.DomainException($"'{setting.Label}' debe ser numérico.");
            setting.Value = value?.Trim();
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await uow.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        (await Repo.FirstOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
        decimal.TryParse(await GetAsync(key, ct), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
