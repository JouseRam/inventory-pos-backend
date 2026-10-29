using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using InventoryPOS.API.Infrastructure;
using InventoryPOS.Application;
using InventoryPOS.Application.Common;
using InventoryPOS.Infrastructure;
using InventoryPOS.Infrastructure.Identity;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace InventoryPOS.API;

/// <summary>
/// Builds the POS web application. Used by the API's own Program (server / development)
/// and by the desktop register, which hosts the same app in-process on the loopback address.
/// </summary>
public static class PosWebApp
{
    /// <summary>True when hosted inside the desktop register: loopback only, plain HTTP, no Swagger.</summary>
    public const string DesktopFlag = "Pos:Desktop";

    public static WebApplication Build(WebApplicationOptions options, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(options);
        configure?.Invoke(builder);
        var desktop = builder.Configuration.GetValue<bool>(DesktopFlag);

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();

        builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
            // Controllers live in this assembly even when another executable (the desktop register) is the entry point.
            .AddApplicationPart(typeof(PosWebApp).Assembly)
            .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ExceptionHandler>();

        // ── Authentication: JWT signed with Jwt:Key (user-secrets, environment or the desktop key store; never committed) ──
        var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Key))
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException("Configure Jwt:Key (32+ characters) before starting the API.");
            // Development without a configured key: a random key per run; tokens die on restart.
            jwt.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            builder.Services.PostConfigure<JwtOptions>(o => o.Key = jwt.Key);
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    NameClaimType = PosClaims.UserName,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });
        builder.Services.AddAuthorization(o =>
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (allowedOrigins.Length > 0) p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }));

        if (!desktop)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1", new OpenApiInfo { Title = "Beauty POS API", Version = "v1" });
                o.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
                var scheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                };
                o.AddSecurityDefinition("Bearer", scheme);
                o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
            });
        }

        var app = builder.Build();

        app.UseExceptionHandler();
        if (!desktop)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapFallbackToFile("index.html").AllowAnonymous();
        return app;
    }

    /// <summary>Migrates (in development or when Database:AutoMigrate is on) and seeds the database.</summary>
    public static async Task InitializeAsync(WebApplication app, CancellationToken ct = default)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
            .InitializeAsync(migrate: app.Environment.IsDevelopment(), ct);
    }
}
