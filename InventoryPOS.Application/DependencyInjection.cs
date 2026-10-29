using FluentValidation;
using InventoryPOS.Application.Features.CashRegister;
using InventoryPOS.Application.Features.Catalog;
using InventoryPOS.Application.Features.Inventory;
using InventoryPOS.Application.Features.Printing;
using InventoryPOS.Application.Features.Products;
using InventoryPOS.Application.Features.Reports;
using InventoryPOS.Application.Features.Sales;
using InventoryPOS.Application.Features.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryPOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<BrandService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<PurchaseService>();
        services.AddScoped<SaleService>();
        services.AddScoped<CashSessionService>();
        services.AddScoped<SettingService>();
        services.AddScoped<ReportService>();
        services.AddScoped<PrintingService>();
        return services;
    }
}
