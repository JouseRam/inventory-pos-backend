using InventoryPOS.API.Authorization;
using InventoryPOS.Application.Features.Sync;
using InventoryPOS.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryPOS.API.Controllers;

/// <summary>
/// Register ↔ central server sync. Protocol endpoints are called by registers with their device token (not a user JWT);
/// the rest serve the screens of each side.
/// </summary>
[ApiController]
[Route("api/sync")]
[RequireModule(PosModules.Settings)]
public class SyncController(ISyncServer server, ISyncClient client) : ControllerBase
{
    private const string TokenHeader = "X-Device-Token";

    // ── Protocol (central) ──

    [HttpPost("pair")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<PairResponse> Pair(PairRequest request, CancellationToken ct) => server.PairAsync(request, ct);

    [HttpGet("pull")]
    [AllowAnonymous]
    public async Task<IActionResult> Pull([FromQuery] DateTime? since, CancellationToken ct)
    {
        var device = await server.AuthenticateAsync(Request.Headers[TokenHeader], ct);
        return device == null ? Unauthorized() : Ok(await server.PullAsync(device.Value, since, ct));
    }

    [HttpPost("push")]
    [AllowAnonymous]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> Push(PushRequest request, CancellationToken ct)
    {
        var device = await server.AuthenticateAsync(Request.Headers[TokenHeader], ct);
        return device == null ? Unauthorized() : Ok(await server.PushAsync(device.Value, request, ct));
    }

    // ── Registers administered on the web (super admins) ──

    [HttpGet("devices")]
    [RequireModule(PosModules.Users)]
    public Task<IReadOnlyList<DeviceDto>> Devices(CancellationToken ct) => server.ListDevicesAsync(ct);

    [HttpPost("devices")]
    [RequireModule(PosModules.Users)]
    public Task<DeviceDto> CreateDevice(DeviceCreateDto dto, CancellationToken ct) => server.CreateDeviceAsync(dto, ct);

    [HttpDelete("devices/{id:int}")]
    [RequireModule(PosModules.Users)]
    public async Task<IActionResult> RevokeDevice(int id, CancellationToken ct)
    {
        await server.RevokeDeviceAsync(id, ct);
        return NoContent();
    }

    // ── This installation ──

    /// <summary>Role and sync state; every screen reads it to adapt (catalog locked on a paired register, no selling on the web).</summary>
    [HttpGet("status")]
    [AnyUser]
    public Task<SyncStatusDto> Status(CancellationToken ct) => client.GetStatusAsync(ct);

    [HttpPost("pair-device")]
    public Task<SyncStatusDto> PairDevice(PairDeviceDto dto, CancellationToken ct) => client.PairAsync(dto, ct);

    [HttpPost("run")]
    [AnyUser]
    public Task<SyncStatusDto> Run(CancellationToken ct) => client.SyncNowAsync(ct);
}
