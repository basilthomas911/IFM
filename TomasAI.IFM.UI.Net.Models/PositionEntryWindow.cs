using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Defines the operator's position-entry window in Toronto/New York time.
/// Closing an existing position is deliberately not restricted by this policy.
/// </summary>
public static class PositionEntryWindow
{
    public static readonly TimeOnly OpensAt = MarketDataFeedMonitoringWindow.OpensAt;
    public static readonly TimeOnly ClosesAt = MarketDataFeedMonitoringWindow.ClosesAt;

    /// <summary>Gets whether a new position may be opened at the supplied instant.</summary>
    public static bool IsOpen(DateTimeOffset utcNow)
        => MarketDataFeedMonitoringWindow.IsOpen(utcNow);

    /// <summary>
    /// Gets the UTC start of the currently open entry window, or <see langword="null"/>
    /// when the supplied instant is outside the window.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
        => MarketDataFeedMonitoringWindow.GetCurrentStartUtc(utcNow);
}
