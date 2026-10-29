namespace InventoryPOS.Application.Common;

public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsSuperAdmin { get; }

    /// <summary>UserId, or throws when the request is anonymous.</summary>
    string RequireUserId() => UserId ?? throw new ForbiddenException("Se requiere un usuario autenticado.");
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    /// <summary>Current date in the store's time zone.</summary>
    DateTime LocalToday { get; }
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);

    /// <summary>UTC bounds [from, to) covering whole local days from..to inclusive.</summary>
    (DateTime FromUtc, DateTime ToUtc) LocalDaysToUtc(DateTime fromLocal, DateTime toLocal) =>
        (ToUtc(fromLocal.Date), ToUtc(toLocal.Date.AddDays(1)));
}

public interface IFolioGenerator
{
    /// <summary>Reserves the next folio of a series; call it inside the business transaction.</summary>
    Task<string> NextAsync(string series, CancellationToken ct = default);
}
