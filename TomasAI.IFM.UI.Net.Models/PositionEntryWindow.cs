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
        var easternNow = EasternTime.FromUtc(utcNow);
        return IsTradingDay(easternNow.DayOfWeek)
            && TimeOnly.FromDateTime(easternNow.DateTime) >= OpensAt
            && TimeOnly.FromDateTime(easternNow.DateTime) < ClosesAt;
    }

    /// <summary>
    /// Gets the UTC start of the currently open entry window, or <see langword="null"/>
    /// when the supplied instant is outside the window.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
    {
        var easternNow = EasternTime.FromUtc(utcNow);
        if (!IsOpen(utcNow))
            return null;

        var easternStart = new DateTime(
            easternNow.Year,
            easternNow.Month,
            easternNow.Day,
            OpensAt.Hour,
            OpensAt.Minute,
            0,
            DateTimeKind.Unspecified);
        return new DateTimeOffset(EasternTime.ToUtc(easternStart));
    }

    static bool IsTradingDay(DayOfWeek dayOfWeek)
        => dayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
}
