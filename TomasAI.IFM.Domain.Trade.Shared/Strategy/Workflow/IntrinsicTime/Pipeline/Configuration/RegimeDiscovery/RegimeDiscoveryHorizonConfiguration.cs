using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures one observation timeframe used by a strategy horizon.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryTimeFrameConfiguration
{
    /// <summary>Gets the observation timeframe.</summary>
    [Key(0)] public TimeFrameType TimeFrame { get; init; }
    /// <summary>Gets its non-negative contribution weight.</summary>
    [Key(1)] public decimal Weight { get; init; }
    /// <summary>Gets whether evidence from this timeframe is required.</summary>
    [Key(2)] public bool IsRequired { get; init; }
    /// <summary>Gets the maximum accepted signal age in seconds.</summary>
    [Key(3)] public int MaximumAgeSeconds { get; init; }
}

/// <summary>Maps one workflow horizon to its configured observation timeframes.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryHorizonConfiguration
{
    /// <summary>Gets the Daily, Weekly, or Monthly target horizon.</summary>
    [Key(0)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the enabled observation timeframe definitions.</summary>
    [Key(1)] public RegimeDiscoveryTimeFrameConfiguration[] TimeFrames { get; init; } = [];

    /// <summary>Creates the approved V1 default mapping for a target horizon.</summary>
    /// <param name="targetHorizon">Daily, Weekly, or Monthly workflow horizon.</param>
    /// <returns>The immutable default horizon configuration.</returns>
    public static RegimeDiscoveryHorizonConfiguration CreateDefault(TimeFrameType targetHorizon) =>
        targetHorizon switch
        {
            TimeFrameType.Daily => new()
            {
                TargetHorizon = targetHorizon,
                TimeFrames =
                [
                    Frame(TimeFrameType.FifteenMinutes, 0.45m, true, 45 * 60),
                    Frame(TimeFrameType.OneHour, 0.35m, true, 3 * 60 * 60),
                    Frame(TimeFrameType.FiveMinutes, 0.10m, false, 15 * 60),
                    Frame(TimeFrameType.FourHours, 0.10m, false, 12 * 60 * 60)
                ]
            },
            TimeFrameType.Weekly => new()
            {
                TargetHorizon = targetHorizon,
                TimeFrames =
                [
                    Frame(TimeFrameType.OneHour, 0.40m, true, 3 * 60 * 60),
                    Frame(TimeFrameType.FourHours, 0.40m, true, 12 * 60 * 60),
                    Frame(TimeFrameType.FifteenMinutes, 0.10m, false, 45 * 60),
                    Frame(TimeFrameType.Daily, 0.10m, false, 96 * 60 * 60)
                ]
            },
            TimeFrameType.Monthly => new()
            {
                TargetHorizon = targetHorizon,
                TimeFrames =
                [
                    Frame(TimeFrameType.FourHours, 0.45m, true, 12 * 60 * 60),
                    Frame(TimeFrameType.Daily, 0.40m, true, 96 * 60 * 60),
                    Frame(TimeFrameType.OneHour, 0.15m, false, 3 * 60 * 60)
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(targetHorizon), targetHorizon,
                "Regime Discovery supports only Daily, Weekly, or Monthly horizons.")
        };

    static RegimeDiscoveryTimeFrameConfiguration Frame(
        TimeFrameType timeFrame, decimal weight, bool required, int maximumAgeSeconds) => new()
        {
            TimeFrame = timeFrame,
            Weight = weight,
            IsRequired = required,
            MaximumAgeSeconds = maximumAgeSeconds
        };
}
