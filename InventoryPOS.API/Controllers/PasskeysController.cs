using System.Net;
using Fido2NetLib;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Common;
using InventoryPOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryPOS.API.Controllers;

public record PasskeyRegisterDto(string? Name, AuthenticatorAttestationRawResponse Credential);
public record PasskeyLoginDto(string RequestId, AuthenticatorAssertionRawResponse Credential);

/// <summary>
/// Sign-in with Face ID / Touch ID / fingerprint / Windows Hello (passkeys). Each user activates it on their own devices
/// after signing in with their password; afterwards the sign-in screen offers it without typing anything.
/// </summary>
[ApiController]
[Route("api/auth/passkeys")]
public class PasskeysController(PasskeyService passkeys, ICurrentUser currentUser, SettingService settings, IConfiguration config) : ControllerBase
{
    [HttpPost("login/options")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<PasskeyLoginChallenge> LoginOptions(CancellationToken ct) => passkeys.BeginLogin(await SiteAsync(ct));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<AuthResponseDto> Login(PasskeyLoginDto dto, CancellationToken ct) =>
        await passkeys.CompleteLoginAsync(dto.RequestId, dto.Credential, await SiteAsync(ct), ct);

    [HttpGet]
    public Task<IReadOnlyList<PasskeyDto>> Mine(CancellationToken ct) => passkeys.ListAsync(currentUser.RequireUserId(), ct);

    [HttpPost("register/options")]
    public async Task<CredentialCreateOptions> RegisterOptions(CancellationToken ct) =>
        await passkeys.BeginRegistrationAsync(currentUser.RequireUserId(), await SiteAsync(ct), ct);

    [HttpPost("register")]
    public async Task<PasskeyDto> Register(PasskeyRegisterDto dto, CancellationToken ct) =>
        await passkeys.CompleteRegistrationAsync(currentUser.RequireUserId(), dto.Credential, dto.Name, await SiteAsync(ct), ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await passkeys.DeleteAsync(currentUser.RequireUserId(), id, ct);
        return NoContent();
    }

    /// <summary>
    /// The passkey belongs to the domain the page was opened on (annasbeautysupplies.somee.com, or localhost when testing).
    /// Browsers refuse passkeys on an IP address, so the desktop register at 127.0.0.1 keeps signing in with the password.
    /// "Passkeys:Domain" pins the domain when the site answers on several names.
    /// </summary>
    private async Task<PasskeySite> SiteAsync(CancellationToken ct)
    {
        var host = Request.Host;
        var domain = config["Passkeys:Domain"] is { Length: > 0 } pinned ? pinned : host.Host;
        if (IPAddress.TryParse(domain.Trim('[', ']'), out _))
            throw new DomainException("El acceso con Face ID o huella funciona desde la dirección web del sistema, no desde una dirección IP.");
        var hostPart = host.Port is { } port ? $"{host.Host}:{port}" : host.Host;
        // Behind IIS (Somee) the request may arrive as http while the browser shows https: both are the same site.
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $"https://{hostPart}", $"http://{hostPart}" };
        foreach (var extra in config.GetSection("Passkeys:Origins").Get<string[]>() ?? []) origins.Add(extra.TrimEnd('/'));
        var name = (await settings.GetAsync(SettingKeys.BusinessName, ct))?.Trim() is { Length: > 0 } business ? business : "Punto de venta";
        return new PasskeySite(domain, name, origins);
    }
}
