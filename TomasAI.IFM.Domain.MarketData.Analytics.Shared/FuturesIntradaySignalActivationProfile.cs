namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Defines the authoritative intraday signal actors that the UI starts for one active futures contract.
/// </summary>
public static class FuturesIntradaySignalActivationProfile
{
    public const int RsiPeriodLength = 13;
    public const int AtrPeriodLength = 14;
    public const int AdxPeriodLength = 14;

    static readonly IReadOnlyList<TimeFrameType> ConfiguredTimeFrames = Array.AsReadOnly(
    [
        TimeFrameType.FifteenSeconds,
        TimeFrameType.OneMinute,
        TimeFrameType.FiveMinutes,
        TimeFrameType.FifteenMinutes,
        TimeFrameType.OneHour,
        TimeFrameType.FourHours
    ]);

    /// <summary>Gets the exact intraday timeframes that are automatically activated.</summary>
    public static IReadOnlyList<TimeFrameType> TimeFrames => ConfiguredTimeFrames;

    /// <summary>Creates all four signal identities for each configured intraday timeframe.</summary>
    public static IReadOnlyList<FuturesIntradaySignalActivation> Create(
        string contractId,
        DateOnly valueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (valueDate == DateOnly.MinValue || valueDate == DateOnly.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        return ConfiguredTimeFrames
            .Select(timeFrame => new FuturesIntradaySignalActivation(
                timeFrame,
                FuturesRsiSignalEntityId.Create(contractId, valueDate, timeFrame, RsiPeriodLength),
                FuturesAtrSignalEntityId.Create(contractId, valueDate, timeFrame, AtrPeriodLength),
                FuturesAdxSignalEntityId.Create(contractId, valueDate, timeFrame, AdxPeriodLength),
                FuturesMacdSignalEntityId.Create(contractId, valueDate, timeFrame)))
            .ToArray();
    }
}

/// <summary>Contains the four signal actor identities for one intraday timeframe.</summary>
public sealed record FuturesIntradaySignalActivation(
    TimeFrameType TimeFrame,
    FuturesRsiSignalEntityId Rsi,
    FuturesAtrSignalEntityId Atr,
    FuturesAdxSignalEntityId Adx,
    FuturesMacdSignalEntityId Macd);
