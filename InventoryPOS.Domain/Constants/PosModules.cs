namespace InventoryPOS.Domain.Constants;

/// <summary>Permission modules; each user gets CanView/CanEdit per key.</summary>
public static class PosModules
{
    public const string Dashboard = "Dashboard";
    public const string Sales = "Sales";
    public const string CashRegister = "CashRegister";
    public const string Products = "Products";
    public const string Inventory = "Inventory";
    public const string Purchases = "Purchases";
    public const string Customers = "Customers";
    public const string Suppliers = "Suppliers";
    public const string Reports = "Reports";
    public const string Settings = "Settings";
    /// <summary>User administration is reserved to super admins and never granted per module.</summary>
    public const string Users = "Users";

    public record ModuleInfo(string Key, string Label, string Icon);

    public static readonly IReadOnlyList<ModuleInfo> All =
    [
        new(Dashboard, "Dashboard", "bi-speedometer2"),
        new(Sales, "Punto de venta", "bi-cart3"),
        new(CashRegister, "Caja", "bi-cash-stack"),
        new(Products, "Productos", "bi-box-seam"),
        new(Inventory, "Inventario", "bi-clipboard-data"),
        new(Purchases, "Compras", "bi-truck"),
        new(Customers, "Clientes", "bi-people"),
        new(Suppliers, "Proveedores", "bi-building"),
        new(Reports, "Reportes", "bi-graph-up"),
        new(Settings, "Configuración", "bi-gear"),
    ];

    public static bool IsValid(string? key) => All.Any(m => m.Key == key);
}
