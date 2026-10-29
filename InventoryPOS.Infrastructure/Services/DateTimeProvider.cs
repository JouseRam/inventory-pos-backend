using InventoryPOS.Application.Common;

namespace InventoryPOS.Infrastructure.Services;

/// <summary>Converts between UTC (storage) and the store's local time zone (reports, "today").</summary>
public class DateTimeProvider(TimeZoneInfo timeZone) : IDateTimeProvider
{
    public const string DefaultTimeZone = "Central Standard Time (Mexico)";

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalToday => ToLocal(DateTime.UtcNow).Date;

    public DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone), DateTimeKind.Unspecified);

    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);

    /// <summary>Resolves a Windows or IANA id, falling back to Mexico City.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        foreach (var candidate in new[] { id, DefaultTimeZone, "America/Mexico_City" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
