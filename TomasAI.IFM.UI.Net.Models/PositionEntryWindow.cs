using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Defines the operator's position-entry window in Toronto/New York time.
/// Closing an existing position is deliberately not restricted by this policy.
/// </summary>
public static class PositionEntryWindow
{
    public static readonly TimeOnly OpensAt = new(3, 0);
    public static readonly TimeOnly ClosesAt = new(16, 0);

    /// <summary>Gets whether a new position may be opened at the supplied instant.</summary>
    public static bool IsOpen(DateTimeOffset utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTime(
            utcNow,
            FuturesTradingValueDate.MarketTimeZone);
        var easternTime = TimeOnly.FromDateTime(easternNow.DateTime);
        return easternNow.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
            && easternTime >= OpensAt
            && easternTime < ClosesAt;
    }

    /// <summary>
    /// Gets the UTC start of the currently open entry window, or <see langword="null"/>
    /// when the supplied instant is outside the window.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTime(
            utcNow,
            FuturesTradingValueDate.MarketTimeZone);
        if (!IsOpen(utcNow))
            return null;
        var easternStart = DateOnly.FromDateTime(easternNow.DateTime).ToDateTime(
            OpensAt,
            DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(
            easternStart,
            FuturesTradingValueDate.MarketTimeZone);
        return new DateTimeOffset(utcStart, TimeSpan.Zero);
    }
}
