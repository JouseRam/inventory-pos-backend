using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentValidation;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Invoicing;

/// <summary>One saved account per provider; switching keeps the others, so their invoices can still be downloaded and cancelled.</summary>
public record ProviderAccountSummary(string Provider, bool Active, bool Sandbox, bool Ready);

/// <summary>The account of one provider (the active one unless another was asked for), plus every provider available.</summary>
public record InvoicingAccountDto(
    string Provider, bool Active, string? ActiveProvider, IReadOnlyList<ProviderInfo> Providers, IReadOnlyList<ProviderAccountSummary> Accounts,
    bool Sandbox, string? UserName, bool HasPassword, bool PasswordUnreadable,
    string DefaultProductCode, string? Serie,
    string? CsdRfc, string? CsdCertificateNumber, DateTime? CsdValidUntil, DateTime? CsdUploadedAt, bool CsdReady, bool Ready);

/// <summary>Password null keeps the stored one.</summary>
public record InvoicingAccountSaveDto(string Provider, bool Sandbox, string UserName, string? Password, string DefaultProductCode, string? Serie);

public class InvoicingAccountSaveValidator : AbstractValidator<InvoicingAccountSaveDto>
{
    public InvoicingAccountSaveValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().WithMessage("Elige el proveedor de facturación.");
        RuleFor(x => x.UserName).MaximumLength(150);
        RuleFor(x => x.Password).MaximumLength(200);
        RuleFor(x => x.DefaultProductCode).Must(FiscalRules.IsValidProductCode)
            .WithMessage("La clave de producto o servicio del SAT tiene 8 dígitos (ej. 53131600).");
        RuleFor(x => x.Serie).MaximumLength(25).Matches("^[A-Za-z0-9]*$").WithMessage("La serie solo admite letras y números.");
    }
}

/// <summary>What a CSD certificate says about itself, read before it is sent to the provider.</summary>
public record CsdInfo(string Rfc, string CertificateNumber, DateTime ValidFrom, DateTime ValidUntil);

/// <summary>Configures this installation's account with the invoicing provider and uploads the business's CSD.</summary>
public class InvoicingAccountService(
    IUnitOfWork uow, IEnumerable<IInvoiceProvider> providers, ISecretProtector secrets, SettingService settings)
{
    private IRepository<InvoicingAccount> Repo => uow.Repository<InvoicingAccount>();

    /// <summary>The account of the given provider, or of the active one when none is given.</summary>
    public async Task<InvoicingAccountDto> GetAsync(string? provider = null, CancellationToken ct = default)
    {
        var all = await Repo.ListAsync(null, ct);
        var name = provider != null ? Provider(provider).Name : all.FirstOrDefault(a => a.IsActive)?.Provider ?? providers.First().Name;
        return ToDto(all.FirstOrDefault(a => a.Provider == name), name, all);
    }

    /// <summary>Saves the provider's account and makes it the one used for new invoices; other providers keep theirs.</summary>
    public async Task<InvoicingAccountDto> SaveAsync(InvoicingAccountSaveDto dto, CancellationToken ct = default)
    {
        var provider = Provider(dto.Provider);
        var info = provider.Info;
        var all = await Repo.ListAsync(null, ct);
        var account = all.FirstOrDefault(a => a.Provider == provider.Name);
        if (account == null) { Repo.Add(account = new InvoicingAccount { Provider = provider.Name }); all = [.. all, account]; }
        else account.UpdatedAt = DateTime.UtcNow;

        var userName = info.UserNameLabel == null ? "" : dto.UserName.Trim();
        if (info.UserNameLabel != null && userName.Length == 0)
            throw new DomainException($"Captura el {info.UserNameLabel.ToLowerInvariant()}.");
        if (account.ProtectedPassword == null && string.IsNullOrEmpty(dto.Password))
            throw new DomainException($"Captura la {info.PasswordLabel.ToLowerInvariant()}.");
        var sandbox = info.TestKeyPrefix == null ? dto.Sandbox
            : !string.IsNullOrEmpty(dto.Password) ? dto.Password.StartsWith(info.TestKeyPrefix, StringComparison.Ordinal)
            : account.Sandbox;

        // Another environment or user has its own CSD registry at the provider.
        if (account.Sandbox != sandbox || account.UserName != userName)
            account.CsdRfc = account.CsdCertificateNumber = null;
        if (account.CsdRfc == null) { account.CsdValidUntil = null; account.CsdUploadedAt = null; }

        account.Sandbox = sandbox;
        account.UserName = userName;
        if (!string.IsNullOrEmpty(dto.Password)) account.ProtectedPassword = secrets.Protect(dto.Password);
        account.DefaultProductCode = dto.DefaultProductCode.Trim();
        account.Serie = string.IsNullOrWhiteSpace(dto.Serie) ? null : dto.Serie.Trim().ToUpperInvariant();
        foreach (var a in all) a.IsActive = a == account;
        await uow.SaveChangesAsync(ct);
        return ToDto(account, account.Provider, all);
    }

    /// <summary>Calls the provider with the saved credentials.</summary>
    public async Task VerifyAsync(string? provider = null, CancellationToken ct = default)
    {
        var (_, p, credentials) = await RequireCredentialsAsync(provider, ct);
        await p.VerifyAsync(credentials, ct);
    }

    /// <summary>
    /// Checks the certificate, the key and its password locally (so the user learns what is wrong without a round trip),
    /// then hands them to the provider. The files are not stored here.
    /// </summary>
    public async Task<InvoicingAccountDto> UploadCsdAsync(byte[] certificate, byte[] privateKey, string password, CancellationToken ct = default)
    {
        var (account, provider, credentials) = await RequireCredentialsAsync(null, ct);
        if (!provider.Info.UploadsCsd) throw new DomainException(provider.Info.CsdPortalHint ?? "Este proveedor recibe el CSD en su propio portal.");
        var info = ReadCsd(certificate, privateKey, password, DateTime.UtcNow);

        var businessRfc = FiscalRules.Normalize(await settings.GetAsync(SettingKeys.BusinessRfc, ct));
        if (businessRfc == null)
            throw new DomainException("Captura primero el RFC del negocio en Configuración → Negocio.");
        if (businessRfc != info.Rfc)
            throw new DomainException($"El certificado es del RFC {info.Rfc}, pero el negocio está configurado con {businessRfc}.");

        await provider.UploadCsdAsync(credentials, new CsdFiles(info.Rfc, certificate, privateKey, password), ct);
        account.CsdRfc = info.Rfc;
        account.CsdCertificateNumber = info.CertificateNumber;
        account.CsdValidUntil = info.ValidUntil;
        account.CsdUploadedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return ToDto(account, account.Provider, await Repo.ListAsync(null, ct));
    }

    /// <summary>
    /// An account, its provider and the decrypted credentials: the active one for new invoices, or the one an existing
    /// invoice was stamped with. Explains what is missing otherwise.
    /// </summary>
    internal async Task<(InvoicingAccount Account, IInvoiceProvider Provider, ProviderAccount Credentials)> RequireCredentialsAsync(
        string? providerName, CancellationToken ct)
    {
        var account = providerName == null
            ? await Repo.FirstOrDefaultAsync(a => a.IsActive, ct)
            : await Repo.FirstOrDefaultAsync(a => a.Provider == providerName, ct);
        if (account == null || account.ProtectedPassword == null)
            throw new DomainException(providerName == null
                ? "Configura la cuenta de facturación en Configuración → Facturación."
                : $"Esta factura se emitió con {providerName}; vuelve a capturar esa cuenta en Configuración → Facturación para descargarla o cancelarla.");
        var password = secrets.Unprotect(account.ProtectedPassword)
            ?? throw new DomainException("La contraseña de facturación guardada ya no se puede leer en esta computadora; vuelve a capturarla en Configuración → Facturación.");
        return (account, Provider(account.Provider), new ProviderAccount(account.Sandbox, account.UserName, password));
    }

    private IInvoiceProvider Provider(string name) =>
        providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException($"El proveedor de facturación '{name}' no está disponible.");

    private bool IsReady(InvoicingAccount a) =>
        a.ProtectedPassword != null && secrets.Unprotect(a.ProtectedPassword) != null && (a.CsdRfc != null || !Provider(a.Provider).Info.UploadsCsd);

    private InvoicingAccountDto ToDto(InvoicingAccount? a, string providerName, IReadOnlyList<InvoicingAccount> all)
    {
        var infos = providers.Select(p => p.Info).ToList();
        var summaries = all.Where(x => providers.Any(p => p.Name == x.Provider))
            .Select(x => new ProviderAccountSummary(x.Provider, x.IsActive, x.Sandbox, IsReady(x))).ToList();
        var activeProvider = all.FirstOrDefault(x => x.IsActive)?.Provider;
        var uploadsCsd = Provider(providerName).Info.UploadsCsd;
        if (a == null)
            return new(providerName, false, activeProvider, infos, summaries, true, null, false, false, "01010101", null,
                null, null, null, null, !uploadsCsd, false);
        var unreadable = a.ProtectedPassword != null && secrets.Unprotect(a.ProtectedPassword) == null;
        return new(a.Provider, a.IsActive, activeProvider, infos, summaries, a.Sandbox, a.UserName, a.ProtectedPassword != null, unreadable,
            a.DefaultProductCode, a.Serie, a.CsdRfc, a.CsdCertificateNumber, a.CsdValidUntil, a.CsdUploadedAt,
            a.CsdRfc != null || !uploadsCsd, IsReady(a));
    }

    /// <summary>Reads a SAT CSD: the .cer (DER) and the .key (encrypted PKCS#8 DER) that must open with the password and match it.</summary>
    public static CsdInfo ReadCsd(byte[] certificate, byte[] privateKey, string password, DateTime nowUtc)
    {
        X509Certificate2 cert;
        try { cert = new X509Certificate2(certificate); }
        catch (CryptographicException) { throw new DomainException("El archivo .cer no es un certificado válido."); }

        using (cert)
        {
            using var publicKey = cert.GetRSAPublicKey() ?? throw new DomainException("El certificado no contiene una llave RSA.");
            using var rsa = RSA.Create();
            try { rsa.ImportEncryptedPkcs8PrivateKey(password, privateKey, out _); }
            catch (CryptographicException) { throw new DomainException("No se pudo abrir el archivo .key: revisa que la contraseña de la llave privada sea la correcta."); }

            var certModulus = publicKey.ExportParameters(false).Modulus!;
            var keyModulus = rsa.ExportParameters(false).Modulus!;
            if (!certModulus.AsSpan().SequenceEqual(keyModulus))
                throw new DomainException("El archivo .key no corresponde al certificado .cer.");

            if (cert.NotAfter.ToUniversalTime() < nowUtc)
                throw new DomainException($"El certificado venció el {cert.NotAfter:dd/MM/yyyy}; tramita uno nuevo en el SAT.");

            var rfc = RfcOf(cert) ?? throw new DomainException("No se encontró el RFC dentro del certificado; ¿es un CSD del SAT?");
            return new CsdInfo(rfc, CertificateNumber(cert), cert.NotBefore.ToUniversalTime(), cert.NotAfter.ToUniversalTime());
        }
    }

    /// <summary>The SAT puts "RFC / RFC del representante" in the x500UniqueIdentifier attribute (2.5.4.45).</summary>
    private static string? RfcOf(X509Certificate2 cert)
    {
        foreach (var rdn in cert.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.GetSingleElementType().Value != "2.5.4.45") continue;
            var value = rdn.GetSingleElementValue();
            var rfc = value?.Split('/')[0].Trim().ToUpperInvariant();
            if (FiscalRules.IsValidRfc(rfc)) return rfc;
        }
        return null;
    }

    /// <summary>The 20-digit certificate number is the serial number read as ASCII.</summary>
    private static string CertificateNumber(X509Certificate2 cert)
    {
        var serial = Convert.FromHexString(cert.SerialNumber);
        var ascii = Encoding.ASCII.GetString(serial);
        return ascii.All(char.IsDigit) ? ascii : cert.SerialNumber;
    }
}
