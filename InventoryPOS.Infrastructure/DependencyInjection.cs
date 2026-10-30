using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Printing;
using InventoryPOS.Application.Features.Reports;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Interfaces;
using InventoryPOS.Infrastructure.Identity;
using InventoryPOS.Infrastructure.Persistence;
using InventoryPOS.Infrastructure.Persistence.Seed;
using InventoryPOS.Infrastructure.Printing;
using InventoryPOS.Infrastructure.Services;
using InventoryPOS.Infrastructure.Sync;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var database = config.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(database);
        var role = PosEnvironment.ResolveRole(config);
        var syncInterceptor = new SyncSaveChangesInterceptor(role);
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.AddInterceptors(syncInterceptor);
            if (database.IsSqlite)
                options.UseSqlite(config.GetConnectionString("Sqlite"), sql => sql.MigrationsAssembly(DatabaseOptions.SqliteMigrationsAssembly));
            else
                options.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                o.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IFolioGenerator, FolioGenerator>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddSingleton<ITicketPrinter, EscPosTicketPrinter>();
        services.AddScoped<DbSeeder>();

        // ── Sync between the register and the central server ──
        services.AddScoped<IPosEnvironment, PosEnvironment>();
        services.AddScoped<SyncMapper>();
        services.AddScoped<SyncApplier>();
        services.AddScoped<ISyncServer, SyncServer>();
        services.AddScoped<ISyncClient, SyncClient>();
        services.AddHttpClient(SyncClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(60));
        if (role == PosRole.Register) services.AddHostedService<SyncWorker>();

        services.AddScoped<DatabaseInitializer>();
        if (database.IsSqlite)
        {
            services.AddSingleton<SqliteBackupService>();
            services.AddHostedService(sp => sp.GetRequiredService<SqliteBackupService>());
            services.AddSingleton<IBackupService>(sp => sp.GetRequiredService<SqliteBackupService>());
        }
        else
        {
            services.AddSingleton<IBackupService, NoBackupService>();
        }
        services.AddSingleton<IDateTimeProvider>(new DateTimeProvider(DateTimeProvider.ResolveTimeZone(config["Pos:TimeZone"])));
        return services;
    }
}
