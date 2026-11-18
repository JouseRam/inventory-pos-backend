using System.Security.Cryptography;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Users;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace InventoryPOS.Infrastructure.Identity;

/// <summary>The site a passkey belongs to: its domain (RP ID), the name shown on the device, and the page origins allowed to use it.</summary>
public record PasskeySite(string Domain, string Name, IReadOnlySet<string> Origins);

public record PasskeyDto(int Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt);

/// <summary>A sign-in challenge handed to the browser; the id brings back the matching options when the device answers.</summary>
public record PasskeyLoginChallenge(string RequestId, object Options);

/// <summary>
/// Sign-in with passkeys (WebAuthn): Face ID / Touch ID on iPhone, fingerprint on Android, Windows Hello.
/// The device keeps the private key and only unlocks it with the user's biometric; the server stores the public key and
/// checks a signature over a one-time challenge. Passkeys are discoverable, so the sign-in needs no user name.
/// </summary>
public class PasskeyService(ApplicationDbContext db, UserManager<AppUser> users, IIdentityService identity, IMemoryCache cache)
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private const string VerifyFailed = "No se pudo verificar con este dispositivo. Intenta de nuevo o entra con tu contraseña.";

    private static Fido2 Fido(PasskeySite site) => new(new Fido2Configuration
    {
        ServerDomain = site.Domain,
        ServerName = site.Name,
        Origins = site.Origins.ToHashSet(),
        TimestampDriftTolerance = 300000,
    });

    // ── Register a passkey on this device (signed-in user) ──

    public async Task<CredentialCreateOptions> BeginRegistrationAsync(string userId, PasskeySite site, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new ForbiddenException("La cuenta no está activa.");
        var existing = await db.UserPasskeys.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.CredentialId).ToListAsync(ct);
        var options = Fido(site).RequestNewCredential(
            new Fido2User
            {
                Id = Encoding.UTF8.GetBytes(user.Id),
                Name = user.UserName ?? user.Id,
                DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? user.Id : user.FullName,
            },
            existing.Select(id => new PublicKeyCredentialDescriptor(id)).ToList(), // one passkey per device
            new AuthenticatorSelection
            {
                RequireResidentKey = true,                               // discoverable: no user name at sign-in
                UserVerification = UserVerificationRequirement.Required, // the biometric (or device PIN) is mandatory
            },
            AttestationConveyancePreference.None,
            new AuthenticationExtensionsClientInputs());
        cache.Set(RegistrationKey(userId), options.ToJson(), ChallengeLifetime);
        return options;
    }

    public async Task<PasskeyDto> CompleteRegistrationAsync(string userId, AuthenticatorAttestationRawResponse response, string? name,
        PasskeySite site, CancellationToken ct = default)
    {
        if (!cache.TryGetValue(RegistrationKey(userId), out string? json) || json == null)
            throw new DomainException("La solicitud venció. Vuelve a intentarlo.");
        cache.Remove(RegistrationKey(userId));
        var options = CredentialCreateOptions.FromJson(json);

        Fido2.CredentialMakeResult made;
        try
        {
            made = await Fido(site).MakeNewCredentialAsync(response, options,
                async (args, token) => !await db.UserPasskeys.AnyAsync(p => p.CredentialId == args.CredentialId, token),
                cancellationToken: ct);
        }
        catch (Fido2VerificationException) { throw new DomainException(VerifyFailed); }
        if (made.Result is not { } credential) throw new DomainException(VerifyFailed);

        var passkey = new UserPasskey
        {
            UserId = userId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignCount = credential.Counter,
            AaGuid = credential.Aaguid,
            Name = string.IsNullOrWhiteSpace(name) ? "Este dispositivo" : name.Trim()[..Math.Min(name.Trim().Length, 100)],
        };
        db.UserPasskeys.Add(passkey);
        await db.SaveChangesAsync(ct);
        return ToDto(passkey);
    }

    public async Task<IReadOnlyList<PasskeyDto>> ListAsync(string userId, CancellationToken ct = default) =>
        (await db.UserPasskeys.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(ct))
            .OrderByDescending(p => p.CreatedAt).Select(ToDto).ToList();

    public async Task DeleteAsync(string userId, int id, CancellationToken ct = default)
    {
        var passkey = await db.UserPasskeys.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct)
            ?? throw NotFoundException.For("Acceso con Face ID", id);
        db.UserPasskeys.Remove(passkey);
        await db.SaveChangesAsync(ct);
    }

    // ── Sign in with a passkey (anonymous) ──

    public PasskeyLoginChallenge BeginLogin(PasskeySite site)
    {
        // No allowed list: the device offers the passkeys it holds for this site.
        var options = Fido(site).GetAssertionOptions([], UserVerificationRequirement.Required, new AuthenticationExtensionsClientInputs());
        var requestId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        cache.Set(LoginKey(requestId), options.ToJson(), ChallengeLifetime);
        return new PasskeyLoginChallenge(requestId, options);
    }

    public async Task<AuthResponseDto> CompleteLoginAsync(string requestId, AuthenticatorAssertionRawResponse response, PasskeySite site,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(requestId) || !cache.TryGetValue(LoginKey(requestId), out string? json) || json == null)
            throw new DomainException("La solicitud venció. Vuelve a intentarlo.");
        cache.Remove(LoginKey(requestId)); // each challenge is good for one answer only
        var options = AssertionOptions.FromJson(json);

        var passkey = await db.UserPasskeys.FirstOrDefaultAsync(p => p.CredentialId == response.Id, ct)
            ?? throw new DomainException("Este dispositivo no tiene activado el acceso. Entra con tu contraseña y actívalo.");

        AssertionVerificationResult verified;
        try
        {
            verified = await Fido(site).MakeAssertionAsync(response, options, passkey.PublicKey, (uint)passkey.SignCount,
                (args, _) => Task.FromResult(Encoding.UTF8.GetString(args.UserHandle) == passkey.UserId), cancellationToken: ct);
        }
        catch (Fido2VerificationException) { throw new DomainException(VerifyFailed); }
        if (verified.Status != "ok") throw new DomainException(VerifyFailed);

        passkey.SignCount = verified.Counter;
        passkey.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await identity.SignInVerifiedAsync(passkey.UserId, ct);
    }

    private static string RegistrationKey(string userId) => "passkey:register:" + userId;
    private static string LoginKey(string requestId) => "passkey:login:" + requestId;

    private static PasskeyDto ToDto(UserPasskey p) => new(p.Id, p.Name, p.CreatedAt, p.LastUsedAt);
}
