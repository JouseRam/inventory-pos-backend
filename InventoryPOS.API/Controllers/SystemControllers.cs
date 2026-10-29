using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Reports;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryPOS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IIdentityService identity, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<AuthResponseDto> Login(LoginDto dto, CancellationToken ct) => identity.LoginAsync(dto, ct);

    /// <summary>Tells the sign-in screen whether this is a fresh install that needs its first admin.</summary>
    [HttpGet("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupStatus(CancellationToken ct) => Ok(new { needsSetup = await identity.NeedsSetupAsync(ct) });

    [HttpPost("setup")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<AuthResponseDto> Setup(FirstAdminDto dto, CancellationToken ct) => identity.SetupFirstAdminAsync(dto, ct);

    [HttpGet("me")]
    public Task<ProfileDto> Me(CancellationToken ct) => identity.GetProfileAsync(currentUser.RequireUserId(), ct);
}

[ApiController]
[Route("api/users")]
[RequireModule(PosModules.Users)]
public class UsersController(IIdentityService identity) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> GetAll(CancellationToken ct) => identity.ListAsync(ct);

    [HttpGet("modules")]
    public IReadOnlyList<PosModules.ModuleInfo> Modules() => PosModules.All;

    [HttpPost]
    public Task<UserDto> Create(UserCreateDto dto, CancellationToken ct) => identity.CreateAsync(dto, ct);

    [HttpPut("{id}")]
    public Task<UserDto> Update(string id, UserUpdateDto dto, CancellationToken ct) => identity.UpdateAsync(id, dto, ct);

    [HttpPut("{id}/permissions")]
    public Task<UserDto> SetPermissions(string id, List<ModulePermissionDto> permissions, CancellationToken ct) =>
        identity.SetPermissionsAsync(id, permissions, ct);
}

[ApiController]
[Route("api/settings")]
[RequireModule(PosModules.Settings)]
public class SettingsController(SettingService service) : ControllerBase
{
    /// <summary>Any signed-in user can read settings (business name and ticket data are needed to print).</summary>
    [HttpGet]
    [AnyUser]
    public Task<IReadOnlyList<SettingDto>> GetAll(CancellationToken ct) => service.ListAsync(ct);

    [HttpPut]
    public Task<IReadOnlyList<SettingDto>> Update(Dictionary<string, string?> values, CancellationToken ct) => service.UpdateAsync(values, ct);
}

[ApiController]
[Route("api/system")]
[RequireModule(PosModules.Settings)]
public class SystemController(IBackupService backups) : ControllerBase
{
    [HttpGet("backups")]
    public BackupStatusDto Backups() => backups.GetStatus();

    [HttpPost("backups")]
    public Task<BackupFileDto> BackupNow(CancellationToken ct) => backups.BackupNowAsync(ct);
}

[ApiController]
[Route("api/catalogs")]
public class CatalogsController : ControllerBase
{
    /// <summary>Fixed lists the UI needs for selects.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        PaymentMethods = PaymentMethods.All,
        Units = UnitsOfMeasure.All,
        CashMovementTypes = CashMovementTypes.All,
        MovementTypes = MovementTypes.All,
        Modules = PosModules.All,
    });
}

[ApiController]
[Route("api/reports")]
[RequireModule(PosModules.Reports)]
public class ReportsController(ReportService service) : ControllerBase
{
    [HttpGet("dashboard")]
    [RequireModule(PosModules.Dashboard)]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => service.GetDashboardAsync(ct);

    [HttpGet("sales-by-day")]
    public Task<IReadOnlyList<SalesByDayDto>> SalesByDay([FromQuery] int days = 30, CancellationToken ct = default) => service.GetSalesByDayAsync(days, ct);

    [HttpGet("top-products")]
    public Task<IReadOnlyList<TopProductDto>> TopProducts([FromQuery] int days = 30, [FromQuery] int limit = 10, CancellationToken ct = default) =>
        service.GetTopProductsAsync(days, limit, ct);

    [HttpGet("by-category")]
    public Task<IReadOnlyList<GroupTotalDto>> ByCategory([FromQuery] int days = 30, CancellationToken ct = default) => service.GetByCategoryAsync(days, ct);

    [HttpGet("by-brand")]
    public Task<IReadOnlyList<GroupTotalDto>> ByBrand([FromQuery] int days = 30, CancellationToken ct = default) => service.GetByBrandAsync(days, ct);

    [HttpGet("payment-methods")]
    public Task<IReadOnlyList<PaymentMethodTotalDto>> PaymentMethods([FromQuery] int days = 30, CancellationToken ct = default) =>
        service.GetPaymentMethodsAsync(days, ct);

    [HttpGet("summary")]
    public Task<SalesSummaryDto> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        service.GetSummaryAsync(from, to, ct);
}
