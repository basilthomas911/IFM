using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Defines the complete, reproducible calculation contract for a Traders Dynamic Index signal.
/// </summary>
[MessagePackObject]
public sealed record FuturesTdiConfiguration
{
    public const int CurrentSchemaVersion = 2;
    public const string StandardConfigurationId = "TDI-13-2-7-34-34-1.6185-SMA-v1";

    [Key(0)] public string ConfigurationId { get; init; } = StandardConfigurationId;
    [Key(1)] public int RsiPeriod { get; init; } = 13;
    [Key(2)] public int PriceLinePeriod { get; init; } = 2;
    [Key(3)] public int SignalLinePeriod { get; init; } = 7;
    [Key(4)] public int MarketBasePeriod { get; init; } = 34;
    [Key(5)] public int VolatilityBandPeriod { get; init; } = 34;
    [Key(6)] public double VolatilityBandDeviation { get; init; } = 1.6185d;
    [Key(7)] public double OversoldLevel { get; init; } = 32d;
    [Key(8)] public double Midline { get; init; } = 50d;
    [Key(9)] public double OverboughtLevel { get; init; } = 68d;
    [Key(10)] public int Version { get; init; } = 1;

    [IgnoreMember]
    public int RequiredRsiSamples => Math.Max(
        Math.Max(PriceLinePeriod, SignalLinePeriod),
        Math.Max(MarketBasePeriod, VolatilityBandPeriod));

    public static FuturesTdiConfiguration Standard { get; } = new();

    /// <summary>Returns whether the period is supported by the intraday TDI workflow.</summary>
    public static bool IsSupportedIntraday(TimeFrameType timePeriod)
        => FuturesIntradaySignalActivationProfile.TimeFrames.Contains(timePeriod);
}
