namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Defines the ADX-style day-based ATR command horizons.</summary>
public static class FuturesAtrDailySignalActivationProfile
{
    static readonly IReadOnlyList<TimeFrameType> ConfiguredTimeFrames = Array.AsReadOnly(
    [
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    ]);

    /// <summary>Gets the supported day-based horizon identities.</summary>
    public static IReadOnlyList<TimeFrameType> TimeFrames => ConfiguredTimeFrames;

    /// <summary>Determines whether a timeframe belongs to the day-based ATR command family.</summary>
    public static bool IsSupported(TimeFrameType timeFrame) => ConfiguredTimeFrames.Contains(timeFrame);
}
