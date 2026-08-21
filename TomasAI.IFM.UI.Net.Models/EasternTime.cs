namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Defines the UI's authoritative Toronto/New York time boundary.
/// Backend instants are UTC; UI clock values are Eastern time.
/// </summary>
public static class EasternTime
{
    const string WindowsTimeZoneId = "Eastern Standard Time";
    const string IanaTimeZoneId = "America/Toronto";

    /// <summary>Gets the Eastern time zone, including its daylight-saving rules.</summary>
    public static TimeZoneInfo Zone { get; } = ResolveTimeZone();

    /// <summary>Converts a backend UTC timestamp to an Eastern UI clock value.</summary>
    /// <remarks>
    /// Message serializers can return a UTC wire value with <see cref="DateTimeKind.Unspecified"/>;
    /// unspecified backend values are therefore interpreted as UTC by this inbound method.
    /// </remarks>
    public static DateTime FromUtc(DateTime backendUtc)
    {
        if (backendUtc == DateTime.MinValue || backendUtc == DateTime.MaxValue)
            return backendUtc;

        var utc = backendUtc.Kind switch
        {
            DateTimeKind.Utc => backendUtc,
            _ => DateTime.SpecifyKind(backendUtc, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }

    /// <summary>Converts an optional backend UTC timestamp to Eastern UI time.</summary>
    public static DateTime? FromUtc(DateTime? backendUtc)
        => backendUtc.HasValue ? FromUtc(backendUtc.Value) : null;

    /// <summary>Converts a backend instant to Eastern UI time.</summary>
    public static DateTimeOffset FromUtc(DateTimeOffset backendInstant)
        => backendInstant == DateTimeOffset.MinValue || backendInstant == DateTimeOffset.MaxValue
            ? backendInstant
            : TimeZoneInfo.ConvertTime(backendInstant, Zone);

    /// <summary>Converts an optional backend instant to Eastern UI time.</summary>
    public static DateTimeOffset? FromUtc(DateTimeOffset? backendInstant)
        => backendInstant.HasValue ? FromUtc(backendInstant.Value) : null;

    /// <summary>Converts an Eastern UI clock value to the UTC instant required by backend APIs.</summary>
    /// <remarks>
    /// UTC inputs are returned unchanged so already-normalized infrastructure values cannot be converted twice.
    /// Every non-UTC value is interpreted as a Toronto/New York wall-clock value, independently of the
    /// workstation's configured time zone.
    /// </remarks>
    public static DateTime ToUtc(DateTime uiLocalTime)
    {
        if (uiLocalTime == DateTime.MinValue || uiLocalTime == DateTime.MaxValue)
            return uiLocalTime;
        if (uiLocalTime.Kind == DateTimeKind.Utc)
            return uiLocalTime;

        var easternClock = DateTime.SpecifyKind(uiLocalTime, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(easternClock))
        {
            throw new ArgumentException(
                $"The Eastern time '{easternClock:O}' does not exist because of the daylight-saving transition.",
                nameof(uiLocalTime));
        }

        return TimeZoneInfo.ConvertTimeToUtc(easternClock, Zone);
    }

    /// <summary>
    /// Encodes an Eastern calendar date as a UTC date without applying an offset.
    /// Use this only for APIs whose <see cref="DateTime"/> parameter represents a
    /// date label rather than an instant.
    /// </summary>
    public static DateTime DateToUtc(DateTime easternDate)
        => easternDate is { Year: 1, Month: 1, Day: 1 }
            ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
            : easternDate is { Year: 9999, Month: 12, Day: 31 }
                ? DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc)
                : new DateTime(
                    easternDate.Year,
                    easternDate.Month,
                    easternDate.Day,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

    /// <summary>Converts an optional Eastern UI clock value to UTC.</summary>
    public static DateTime? ToUtc(DateTime? uiLocalTime)
        => uiLocalTime.HasValue ? ToUtc(uiLocalTime.Value) : null;

    /// <summary>Converts an offset-bearing UI instant to UTC.</summary>
    public static DateTimeOffset ToUtc(DateTimeOffset uiInstant)
        => uiInstant == DateTimeOffset.MinValue || uiInstant == DateTimeOffset.MaxValue
            ? uiInstant
            : uiInstant.ToUniversalTime();

    /// <summary>Converts an optional offset-bearing UI instant to UTC.</summary>
    public static DateTimeOffset? ToUtc(DateTimeOffset? uiInstant)
        => uiInstant.HasValue ? ToUtc(uiInstant.Value) : null;

    /// <summary>Gets the current Toronto/New York wall-clock time from a testable time provider.</summary>
    public static DateTime GetNow(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return FromUtc(timeProvider.GetUtcNow().UtcDateTime);
    }

    static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId);
        }
    }
}
