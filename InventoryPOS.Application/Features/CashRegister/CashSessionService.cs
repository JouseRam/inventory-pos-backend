using FluentValidation;
using InventoryPOS.Application.Common;
using InventoryPOS.Domain.Common;
using InventoryPOS.Domain.Constants;
using InventoryPOS.Domain.Entities;
using InventoryPOS.Domain.Interfaces;

namespace InventoryPOS.Application.Features.CashRegister;

public record CashOpenDto(decimal OpeningAmount, string? Notes);
public record CashMovementSaveDto(string Type, decimal Amount, string Reason);
public record CashCloseDto(decimal CountedCash, string? Notes);

public record CashMovementDto(int Id, DateTime CreatedAt, string Type, decimal Amount, string Reason);

public record CashSummaryDto(
    int Id, string Folio, string UserId, string Status, DateTime OpenedAt, DateTime? ClosedAt,
    decimal OpeningAmount, decimal CashSales, decimal CardSales, decimal TransferSales, decimal TotalSales,
    decimal CashIn, decimal CashOut, decimal ExpectedCash, decimal? CountedCash, decimal? Difference,
    int SalesCount, string? Notes, IReadOnlyList<CashMovementDto> Movements);

public record CashSessionListItemDto(int Id, string Folio, string UserId, string Status, DateTime OpenedAt, DateTime? ClosedAt,
    decimal OpeningAmount, decimal? ExpectedCash, decimal? CountedCash, decimal? Difference);

public class CashSessionService(IUnitOfWork uow, IFolioGenerator folios, ICurrentUser currentUser, IDateTimeProvider clock)
{
    public async Task<CashSummaryDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        var session = await uow.CashSessions.GetOpenByUserAsync(currentUser.RequireUserId(), ct);
        return session == null ? null : await GetSummaryAsync(session.Id, ct);
    }

    public async Task<PagedResult<CashSessionListItemDto>> ListAsync(DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        DateTime? fromUtc = from.HasValue ? clock.ToUtc(from.Value.Date) : null;
        DateTime? toUtc = to.HasValue ? clock.ToUtc(to.Value.Date.AddDays(1)) : null;
        var result = await uow.CashSessions.GetPagedAsync(fromUtc, toUtc, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return result.Map(s => new CashSessionListItemDto(s.Id, s.Folio, s.UserId, s.Status, s.OpenedAt, s.ClosedAt,
            s.OpeningAmount, s.ExpectedCash, s.CountedCash, s.Difference));
    }

    public async Task<CashSummaryDto> OpenAsync(CashOpenDto dto, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        if (await uow.CashSessions.GetOpenByUserAsync(userId, ct) != null)
            throw new ConflictException("Ya tienes una caja abierta; ciérrala antes de abrir otra.");

        var id = await uow.ExecuteInTransactionAsync(async () =>
        {
            var session = new CashSession
            {
                Folio = await folios.NextAsync(FolioSeries.CashSession, ct),
                UserId = userId,
                OpeningAmount = Money.Round(dto.OpeningAmount),
                OpenedAt = DateTime.UtcNow,
                Notes = dto.Notes?.Trim(),
            };
            uow.CashSessions.Add(session);
            await uow.SaveChangesAsync(ct);
            return session.Id;
        }, ct);
        return await GetSummaryAsync(id, ct);
    }

    public async Task<CashSummaryDto> AddMovementAsync(int sessionId, CashMovementSaveDto dto, CancellationToken ct = default)
    {
        var session = await LoadOwnOpenSessionAsync(sessionId, ct);
        session.Movements.Add(new CashMovement
        {
            Type = CashMovementTypes.Normalize(dto.Type)!,
            Amount = Money.Round(dto.Amount),
            Reason = dto.Reason.Trim(),
            UserId = currentUser.UserId,
        });
        await uow.SaveChangesAsync(ct);
        return await GetSummaryAsync(sessionId, ct);
    }

    public async Task<CashSummaryDto> GetSummaryAsync(int sessionId, CancellationToken ct = default)
    {
        var session = await uow.CashSessions.GetWithMovementsAsync(sessionId, ct) ?? throw NotFoundException.For("Caja", sessionId);
        var payments = await uow.Sales.GetSessionPaymentsAsync(sessionId, ct);
        var salesCount = payments.Select(p => p.SaleId).Distinct().Count();
        return BuildSummary(session, payments, salesCount);
    }

    /// <summary>Closes the shift storing expected vs counted cash; the difference is counted minus expected.</summary>
    public async Task<CashSummaryDto> CloseAsync(int sessionId, CashCloseDto dto, CancellationToken ct = default)
    {
        var session = await LoadOwnOpenSessionAsync(sessionId, ct);
        var summary = await GetSummaryAsync(sessionId, ct);

        session.Status = CashSessionStatus.Closed;
        session.ClosedAt = DateTime.UtcNow;
        session.ExpectedCash = summary.ExpectedCash;
        session.CountedCash = Money.Round(dto.CountedCash);
        session.Difference = session.CountedCash - session.ExpectedCash;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            session.Notes = string.IsNullOrWhiteSpace(session.Notes) ? dto.Notes.Trim() : $"{session.Notes}\n{dto.Notes.Trim()}";
        session.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return await GetSummaryAsync(sessionId, ct);
    }

    internal static CashSummaryDto BuildSummary(CashSession session, IReadOnlyList<SalePayment> payments, int salesCount)
    {
        decimal ByMethod(string method) => payments.Where(p => p.Method == method).Sum(p => p.Amount);
        var cashSales = ByMethod(PaymentMethods.Cash);
        var cardSales = ByMethod(PaymentMethods.Card);
        var transferSales = ByMethod(PaymentMethods.Transfer);
        var cashIn = session.Movements.Where(m => m.Type == CashMovementTypes.In).Sum(m => m.Amount);
        var cashOut = session.Movements.Where(m => m.Type == CashMovementTypes.Out).Sum(m => m.Amount);
        var expected = session.OpeningAmount + cashSales + cashIn - cashOut;

        return new CashSummaryDto(
            session.Id, session.Folio, session.UserId, session.Status, session.OpenedAt, session.ClosedAt,
            session.OpeningAmount, cashSales, cardSales, transferSales, cashSales + cardSales + transferSales,
            cashIn, cashOut, session.ExpectedCash ?? expected, session.CountedCash, session.Difference,
            salesCount, session.Notes,
            session.Movements.OrderBy(m => m.CreatedAt).Select(m => new CashMovementDto(m.Id, m.CreatedAt, m.Type, m.Amount, m.Reason)).ToList());
    }

    private async Task<CashSession> LoadOwnOpenSessionAsync(int sessionId, CancellationToken ct)
    {
        var session = await uow.CashSessions.GetWithMovementsAsync(sessionId, ct) ?? throw NotFoundException.For("Caja", sessionId);
        if (session.Status != CashSessionStatus.Open) throw new DomainException("La caja ya está cerrada.");
        if (session.UserId != currentUser.UserId && !currentUser.IsSuperAdmin)
            throw new ForbiddenException("Solo el cajero que abrió la caja o un administrador pueden operarla.");
        return session;
    }
}

public class CashOpenValidator : AbstractValidator<CashOpenDto>
{
    public CashOpenValidator()
    {
        RuleFor(x => x.OpeningAmount).GreaterThanOrEqualTo(0).WithMessage("El fondo inicial no puede ser negativo.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class CashMovementSaveValidator : AbstractValidator<CashMovementSaveDto>
{
    public CashMovementSaveValidator()
    {
        RuleFor(x => x.Type).Must(t => CashMovementTypes.Normalize(t) != null)
            .WithMessage($"Tipo inválido. Usa: {string.Join(", ", CashMovementTypes.All)}.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indica el motivo.").MaximumLength(300);
    }
}

public class CashCloseValidator : AbstractValidator<CashCloseDto>
{
    public CashCloseValidator()
    {
        RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0).WithMessage("El efectivo contado no puede ser negativo.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
