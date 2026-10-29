namespace InventoryPOS.Domain.Constants;

public static class SaleStatus
{
    public const string Completed = "Completada";
    public const string Cancelled = "Cancelada";

    public static readonly string[] All = [Completed, Cancelled];
}

public static class PaymentMethods
{
    public const string Cash = "Efectivo";
    public const string Card = "Tarjeta";
    public const string Transfer = "Transferencia";

    public static readonly string[] All = [Cash, Card, Transfer];

    /// <summary>Maps loose input ("efectivo", "TARJETA") to the canonical value, or null if unknown.</summary>
    public static string? Normalize(string? value) =>
        All.FirstOrDefault(m => string.Equals(m, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class MovementTypes
{
    public const string Purchase = "Compra";
    public const string Sale = "Venta";
    public const string SaleCancel = "Cancelación";
    public const string AdjustmentIn = "Ajuste entrada";
    public const string AdjustmentOut = "Ajuste salida";

    public static readonly string[] All = [Purchase, Sale, SaleCancel, AdjustmentIn, AdjustmentOut];
}

public static class CashSessionStatus
{
    public const string Open = "Abierta";
    public const string Closed = "Cerrada";
}

public static class CashMovementTypes
{
    public const string In = "Ingreso";
    public const string Out = "Retiro";

    public static readonly string[] All = [In, Out];

    public static string? Normalize(string? value) =>
        All.FirstOrDefault(m => string.Equals(m, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class UnitsOfMeasure
{
    public const string Piece = "pza";
    public const string Milliliter = "ml";
    public const string Gram = "g";
    public const string Liter = "l";
    public const string Kilogram = "kg";

    public static readonly string[] All = [Piece, Milliliter, Gram, Liter, Kilogram];
}

public static class FolioSeries
{
    public const string Sale = "Sale";
    public const string Purchase = "Purchase";
    public const string CashSession = "CashSession";
}
