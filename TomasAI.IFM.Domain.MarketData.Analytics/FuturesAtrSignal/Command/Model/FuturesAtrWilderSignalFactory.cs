using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>Creates the public ATR signal contract from one Wilder transition.</summary>
public static class FuturesAtrWilderSignalFactory
{
    /// <summary>Creates a lineage-preserving ATR signal for a closed observation.</summary>
    public static FuturesAtrSignalReadModel Create(
        FuturesAtrSignalId signalId,
        FuturesTradeSessionBarReadModel observation,
        FuturesAtrWilderResult result)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(result);
        var atr = result.AtrValue ?? 0m;
        var ratio = atr > 0 ? result.TrueRange / atr : 0m;
        return new FuturesAtrSignalReadModel(
            signalId.ContractId,
            signalId.ValueDate,
            signalId.TimePeriod,
            signalId.PeriodLength,
            signalId.Timestamp,
            observation.Close,
            (double)atr,
            (double)result.TrueRange,
            Direction(result),
            Strength(ratio))
        {
            PreviousAtrValue = (double?)result.PreviousAtrValue,
            AtrBaseline = (double?)result.AtrBaseline,
            AtrRatio = (double?)result.AtrRatio,
            IsWarm = result.IsWarm,
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(
                    observation.MarketSeriesIdentity,
                    MarketAnalyticsSignalKind.Atr,
                    signalId.TimePeriod,
                    $"atr-{signalId.PeriodLength}-wilder-v1"),
                ContractId = observation.ContractId,
                ValueDate = observation.ValueDate,
                ObservationId = observation.ObservationId,
                MarketDataAsOfUtc = observation.LastMarketEventUtc,
                CalculatedAtUtc = observation.CalculatedAtUtc,
                SourceSequence = observation.LastSourceSequence,
                SchemaVersion = 2,
                CalculationVersion = "atr-wilder-ohlc-v1",
                CalculationMethod = observation.CalculationMethod,
                IsValid = observation.IsValid,
                ValidationIssues = observation.ValidationIssues
            }
        };
    }

    static FuturesTrendDirectionType Direction(FuturesAtrWilderResult result)
    {
        if (!result.IsWarm || result.AtrValue is not > 0)
            return FuturesTrendDirectionType.Init;
        if (result.TrueRange > result.AtrValue.Value)
            return FuturesTrendDirectionType.UpTrending;
        if (result.TrueRange < result.AtrValue.Value)
            return FuturesTrendDirectionType.DownTrending;
        return FuturesTrendDirectionType.Flat;
    }

    static FuturesTrendDirectionStrengthType Strength(decimal ratio) => ratio switch
    {
        >= 1.5m => FuturesTrendDirectionStrengthType.High,
        >= 1m => FuturesTrendDirectionStrengthType.Medium,
        _ => FuturesTrendDirectionStrengthType.Low
    };
}
