using FluentValidation;
using InventoryPOS.Application.Common;
using InventoryPOS.Application.Features.Settings;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.Promotions;

public record PromotionSummaryDto(int Id, string Title, string Message, bool HasImage, DateTime CreatedAt, int Recipients, int Sent, int Pending);

public record PromotionRecipientDto(int Id, int CustomerId, string Name, string Phone, string Status, DateTime? SentAt);

/// <summary>WhatsAppText is the message ready to paste: slogan, text, the store's name and how to stop receiving them.</summary>
public record PromotionDto(int Id, string Title, string Message, bool HasImage, DateTime CreatedAt, string WhatsAppText,
    IReadOnlyList<PromotionRecipientDto> Recipients);

/// <summary>Image is base64 (the page shrinks it first); ImageType image/jpeg or image/png. RemoveImage drops the current one.</summary>
public record PromotionSaveDto(string Title, string Message, string? Image, string? ImageType, bool RemoveImage = false);

public record AudienceCustomerDto(int Id, string Name, string Phone, string Type, decimal DiscountPercent, DateTime? LastPurchase);

/// <summary>Who can receive it, and how many customers are left out for lack of consent or phone.</summary>
public record AudienceDto(IReadOnlyList<AudienceCustomerDto> Customers, int WithoutConsent);

public record RecipientsAddDto(List<int> CustomerIds);

public record RecipientStatusDto(string Status);

public class PromotionSaveValidator : AbstractValidator<PromotionSaveDto>
{
    public const int MaxImageBytes = 1_500_000;

    public PromotionSaveValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Escribe el slogan o título de la promoción.").MaximumLength(150);
        RuleFor(x => x.Message).NotEmpty().WithMessage("Escribe el mensaje de la promoción.").MaximumLength(1500);
        RuleFor(x => x.ImageType).Must(t => t is "image/jpeg" or "image/png").When(x => x.Image != null)
            .WithMessage("La imagen debe ser JPG o PNG.");
        RuleFor(x => x.Image).Must(i => i == null || i.Length * 3L / 4 <= MaxImageBytes)
            .WithMessage("La imagen es muy pesada; usa una de menos de 1.5 MB.");
    }
}

/// <summary>
/// Promotions sent one by one through WhatsApp: the page opens each customer's chat with the text ready and the image on
/// the clipboard, and marks it sent. Only customers who agreed to receive promotions and have a phone can be added.
/// </summary>
public class PromotionService(IUnitOfWork uow, SettingService settings, ICurrentUser currentUser)
{
    private IRepository<Promotion> Promotions => uow.Repository<Promotion>();
    private IRepository<PromotionRecipient> Recipients => uow.Repository<PromotionRecipient>();

    public async Task<IReadOnlyList<PromotionSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var promotions = await Promotions.ListAsync(null, ct);
        var recipients = await Recipients.ListAsync(null, ct);
        return promotions.OrderByDescending(p => p.CreatedAt).Select(p =>
        {
            var mine = recipients.Where(r => r.PromotionId == p.Id).ToList();
            return new PromotionSummaryDto(p.Id, p.Title, p.Message, p.Image != null, p.CreatedAt, mine.Count,
                mine.Count(r => r.Status == PromotionRecipientStatus.Sent), mine.Count(r => r.Status == PromotionRecipientStatus.Pending));
        }).ToList();
    }

    public async Task<PromotionDto> GetAsync(int id, CancellationToken ct = default)
    {
        var p = await Promotions.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Promoción", id);
        var recipients = await Recipients.ListAsync(r => r.PromotionId == id, ct);
        return new PromotionDto(p.Id, p.Title, p.Message, p.Image != null, p.CreatedAt, await WhatsAppTextAsync(p, ct),
            recipients.OrderBy(r => r.Status != PromotionRecipientStatus.Pending).ThenBy(r => r.Name)
                .Select(r => new PromotionRecipientDto(r.Id, r.CustomerId, r.Name, r.Phone, r.Status, r.SentAt)).ToList());
    }

    public async Task<PromotionDto> CreateAsync(PromotionSaveDto dto, CancellationToken ct = default)
    {
        var p = new Promotion { UserId = currentUser.UserId };
        Apply(p, dto);
        Promotions.Add(p);
        await uow.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<PromotionDto> UpdateAsync(int id, PromotionSaveDto dto, CancellationToken ct = default)
    {
        var p = await Promotions.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Promoción", id);
        Apply(p, dto);
        p.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await Promotions.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Promoción", id);
        foreach (var r in await Recipients.ListAsync(r => r.PromotionId == id, ct)) Recipients.Remove(r);
        Promotions.Remove(p);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Content, string Type)> ImageAsync(int id, CancellationToken ct = default)
    {
        var p = await Promotions.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Promoción", id);
        return p.Image == null ? throw new NotFoundException("La promoción no tiene imagen.") : (p.Image, p.ImageType ?? "image/jpeg");
    }

    /// <summary>Customers who can receive promotions (active, with phone and consent), optionally by type or text.</summary>
    public async Task<AudienceDto> AudienceAsync(string? type, string? q, CancellationToken ct = default)
    {
        var term = q?.Trim();
        var active = await uow.Repository<Customer>().ListAsync(c => c.IsActive, ct);
        var eligible = active.Where(c => c.AcceptsPromotions && c.Phone != null)
            .Where(c => string.IsNullOrEmpty(type) || c.Type == type)
            .Where(c => string.IsNullOrEmpty(term) || c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || c.Phone!.Contains(term))
            .ToList();
        var ids = eligible.Select(c => c.Id).ToList();
        var lastPurchase = (await uow.Sales.ListAsync(s => s.CustomerId != null && ids.Contains(s.CustomerId.Value) && s.Status == SaleStatus.Completed, ct))
            .GroupBy(s => s.CustomerId!.Value).ToDictionary(g => g.Key, g => g.Max(s => s.CreatedAt));
        return new AudienceDto(
            eligible.OrderBy(c => c.Name).Select(c => new AudienceCustomerDto(c.Id, c.Name, c.Phone!, c.Type, c.DiscountPercent,
                lastPurchase.TryGetValue(c.Id, out var d) ? d : null)).ToList(),
            active.Count(c => !c.AcceptsPromotions || c.Phone == null));
    }

    /// <summary>Adds customers to the promotion; those without consent or phone, or already in it, are left out.</summary>
    public async Task<PromotionDto> AddRecipientsAsync(int id, RecipientsAddDto dto, CancellationToken ct = default)
    {
        _ = await Promotions.GetByIdAsync(id, ct) ?? throw NotFoundException.For("Promoción", id);
        var already = (await Recipients.ListAsync(r => r.PromotionId == id, ct)).Select(r => r.CustomerId).ToHashSet();
        var wanted = dto.CustomerIds.Distinct().Where(c => !already.Contains(c)).ToList();
        var customers = await uow.Repository<Customer>().ListAsync(c => wanted.Contains(c.Id), ct);
        foreach (var c in customers.Where(c => c.IsActive && c.AcceptsPromotions && c.Phone != null))
            Recipients.Add(new PromotionRecipient { PromotionId = id, CustomerId = c.Id, Name = c.Name, Phone = c.Phone! });
        await uow.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PromotionRecipientDto> SetStatusAsync(int id, int recipientId, RecipientStatusDto dto, CancellationToken ct = default)
    {
        if (!PromotionRecipientStatus.All.Contains(dto.Status)) throw new DomainException("Estado inválido.");
        var r = await Recipients.FirstOrDefaultAsync(x => x.Id == recipientId && x.PromotionId == id, ct)
            ?? throw NotFoundException.For("Destinatario", recipientId);
        r.Status = dto.Status;
        r.SentAt = dto.Status == PromotionRecipientStatus.Sent ? DateTime.UtcNow : null;
        r.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return new PromotionRecipientDto(r.Id, r.CustomerId, r.Name, r.Phone, r.Status, r.SentAt);
    }

    public async Task RemoveRecipientAsync(int id, int recipientId, CancellationToken ct = default)
    {
        var r = await Recipients.FirstOrDefaultAsync(x => x.Id == recipientId && x.PromotionId == id, ct)
            ?? throw NotFoundException.For("Destinatario", recipientId);
        Recipients.Remove(r);
        await uow.SaveChangesAsync(ct);
    }

    private static void Apply(Promotion p, PromotionSaveDto dto)
    {
        p.Title = dto.Title.Trim();
        p.Message = dto.Message.Trim();
        if (dto.Image != null)
        {
            try { p.Image = Convert.FromBase64String(dto.Image); }
            catch (FormatException) { throw new DomainException("La imagen no se pudo leer."); }
            p.ImageType = dto.ImageType;
        }
        else if (dto.RemoveImage)
        {
            p.Image = null;
            p.ImageType = null;
        }
    }

    /// <summary>The text for WhatsApp, in its own formatting (*bold*), signed by the store and with a way to opt out.</summary>
    private async Task<string> WhatsAppTextAsync(Promotion p, CancellationToken ct)
    {
        var business = await settings.GetAsync(SettingKeys.BusinessName, ct);
        var signature = string.IsNullOrWhiteSpace(business) ? "" : $"\n\n— {business.Trim()}";
        return $"*{p.Title}*\n\n{p.Message}{signature}\n\n_Si ya no quieres recibir promociones, responde BAJA._";
    }
}
