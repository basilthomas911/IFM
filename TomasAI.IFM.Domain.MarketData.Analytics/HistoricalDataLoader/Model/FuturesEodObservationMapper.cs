using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Model;

/// <summary>Maps one immutable normalized Databento EOD observation to the canonical Daily bar model.</summary>
public static class FuturesEodObservationMapper
{
    /// <summary>Creates the canonical Daily bar consumed by production analytics accumulators.</summary>
    /// <param name="source">The normalized Databento EOD observation.</param>
    /// <returns>A complete Daily trade-session bar with the source observation lineage.</returns>
    public static FuturesTradeSessionBarReadModel ToDailyBar(FuturesEodObservationReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            MarketSeriesIdentity = source.MarketSeriesIdentity,
            ObservationId = source.ObservationId,
            ContractId = source.ContractId,
            ValueDate = source.ValueDate,
            TimeFrame = TimeFrameType.Daily,
            IntervalStartUtc = source.SessionStartUtc,
            IntervalEndUtc = source.SessionEndUtc,
            Open = source.Open,
            High = source.High,
            Low = source.Low,
            Close = source.Close,
            Volume = source.Volume,
            TradeCount = source.TradeCount,
            PriceVolumeSum = source.PriceVolumeSum,
            FirstSourceSequence = source.FirstSourceSequence,
            LastSourceSequence = source.LastSourceSequence,
            FirstMarketEventUtc = source.FirstMarketEventUtc,
            LastMarketEventUtc = source.LastMarketEventUtc,
            CalculatedAtUtc = source.SessionEndUtc > source.LastMarketEventUtc
                ? source.SessionEndUtc
                : source.LastMarketEventUtc,
            SchemaVersion = source.SchemaVersion,
            CalculationVersion = "historical-daily-v1",
            IsComplete = source.IsComplete,
            IsValid = source.IsValid,
            ValidationIssues = [],
            CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        };
    }
}
