using System.Text.RegularExpressions;

namespace InventoryPOS.Application.Features.Invoicing;

/// <summary>Who an entry of a SAT catalog applies to: personas físicas (13-character RFC), morales (12) or both.</summary>
[Flags]
public enum TaxpayerKind
{
    Individual = 1,
    Company = 2,
    Both = Individual | Company,
}

public record SatCatalogEntry(string Code, string Description, TaxpayerKind AppliesTo);

public record SatCatalogDto(string Code, string Description, bool Individual, bool Company);

public record SatCatalogsDto(IReadOnlyList<SatCatalogDto> TaxRegimes, IReadOnlyList<SatCatalogDto> CfdiUses);

/// <summary>The SAT catalogs (CFDI 4.0) a customer's and the business's fiscal data are chosen from.</summary>
public static class SatCatalogs
{
    /// <summary>c_RegimenFiscal.</summary>
    public static readonly IReadOnlyList<SatCatalogEntry> TaxRegimes =
    [
        new("601", "General de Ley Personas Morales", TaxpayerKind.Company),
        new("603", "Personas Morales con Fines no Lucrativos", TaxpayerKind.Company),
        new("605", "Sueldos y Salarios e Ingresos Asimilados a Salarios", TaxpayerKind.Individual),
        new("606", "Arrendamiento", TaxpayerKind.Individual),
        new("607", "Régimen de Enajenación o Adquisición de Bienes", TaxpayerKind.Individual),
        new("608", "Demás ingresos", TaxpayerKind.Individual),
        new("610", "Residentes en el Extranjero sin Establecimiento Permanente en México", TaxpayerKind.Both),
        new("611", "Ingresos por Dividendos (socios y accionistas)", TaxpayerKind.Individual),
        new("612", "Personas Físicas con Actividades Empresariales y Profesionales", TaxpayerKind.Individual),
        new("614", "Ingresos por intereses", TaxpayerKind.Individual),
        new("615", "Régimen de los ingresos por obtención de premios", TaxpayerKind.Individual),
        new("616", "Sin obligaciones fiscales", TaxpayerKind.Individual),
        new("620", "Sociedades Cooperativas de Producción que optan por diferir sus ingresos", TaxpayerKind.Company),
        new("621", "Incorporación Fiscal", TaxpayerKind.Individual),
        new("622", "Actividades Agrícolas, Ganaderas, Silvícolas y Pesqueras", TaxpayerKind.Company),
        new("623", "Opcional para Grupos de Sociedades", TaxpayerKind.Company),
        new("624", "Coordinados", TaxpayerKind.Company),
        new("625", "Actividades Empresariales con ingresos a través de Plataformas Tecnológicas", TaxpayerKind.Individual),
        new("626", "Régimen Simplificado de Confianza", TaxpayerKind.Both),
    ];

    /// <summary>c_UsoCFDI, without the uses a store never issues to a customer (pagos, nómina).</summary>
    public static readonly IReadOnlyList<SatCatalogEntry> CfdiUses =
    [
        new("G01", "Adquisición de mercancías", TaxpayerKind.Both),
        new("G02", "Devoluciones, descuentos o bonificaciones", TaxpayerKind.Both),
        new("G03", "Gastos en general", TaxpayerKind.Both),
        new("I01", "Construcciones", TaxpayerKind.Both),
        new("I02", "Mobiliario y equipo de oficina por inversiones", TaxpayerKind.Both),
        new("I03", "Equipo de transporte", TaxpayerKind.Both),
        new("I04", "Equipo de cómputo y accesorios", TaxpayerKind.Both),
        new("I05", "Dados, troqueles, moldes, matrices y herramental", TaxpayerKind.Both),
        new("I06", "Comunicaciones telefónicas", TaxpayerKind.Both),
        new("I07", "Comunicaciones satelitales", TaxpayerKind.Both),
        new("I08", "Otra maquinaria y equipo", TaxpayerKind.Both),
        new("D01", "Honorarios médicos, dentales y gastos hospitalarios", TaxpayerKind.Individual),
        new("D02", "Gastos médicos por incapacidad o discapacidad", TaxpayerKind.Individual),
        new("D03", "Gastos funerales", TaxpayerKind.Individual),
        new("D04", "Donativos", TaxpayerKind.Individual),
        new("D05", "Intereses reales pagados por créditos hipotecarios (casa habitación)", TaxpayerKind.Individual),
        new("D06", "Aportaciones voluntarias al SAR", TaxpayerKind.Individual),
        new("D07", "Primas por seguros de gastos médicos", TaxpayerKind.Individual),
        new("D08", "Gastos de transportación escolar obligatoria", TaxpayerKind.Individual),
        new("D09", "Depósitos en cuentas para el ahorro, primas con base en planes de pensiones", TaxpayerKind.Individual),
        new("D10", "Pagos por servicios educativos (colegiaturas)", TaxpayerKind.Individual),
        new("S01", "Sin efectos fiscales", TaxpayerKind.Both),
    ];

    public static SatCatalogsDto ToDto()
    {
        static SatCatalogDto Map(SatCatalogEntry e) =>
            new(e.Code, e.Description, e.AppliesTo.HasFlag(TaxpayerKind.Individual), e.AppliesTo.HasFlag(TaxpayerKind.Company));
        return new SatCatalogsDto(TaxRegimes.Select(Map).ToList(), CfdiUses.Select(Map).ToList());
    }
}

/// <summary>Format rules the SAT applies to fiscal data before a CFDI can be stamped.</summary>
public static partial class FiscalRules
{
    /// <summary>Generic RFC for sales to the general public (factura global).</summary>
    public const string GeneralPublicRfc = "XAXX010101000";

    [GeneratedRegex(@"^[A-ZÑ&]{3,4}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[A-Z\d]{2}[A\d]$")]
    private static partial Regex RfcPattern();

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex ZipPattern();

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    public static bool IsValidRfc(string? rfc) => rfc != null && RfcPattern().IsMatch(rfc);

    public static bool IsValidZip(string? zip) => zip != null && ZipPattern().IsMatch(zip);

    /// <summary>Personas morales have a 12-character RFC, personas físicas 13.</summary>
    public static TaxpayerKind KindOf(string rfc) => rfc.Length == 12 ? TaxpayerKind.Company : TaxpayerKind.Individual;

    public static bool IsValidTaxRegime(string? code, string? rfc = null) =>
        Fits(SatCatalogs.TaxRegimes, code, rfc);

    public static bool IsValidCfdiUse(string? code, string? rfc = null) =>
        Fits(SatCatalogs.CfdiUses, code, rfc);

    private static bool Fits(IReadOnlyList<SatCatalogEntry> catalog, string? code, string? rfc)
    {
        var entry = catalog.FirstOrDefault(e => e.Code == code);
        if (entry == null) return false;
        return !IsValidRfc(rfc) || entry.AppliesTo.HasFlag(KindOf(rfc!));
    }
}
