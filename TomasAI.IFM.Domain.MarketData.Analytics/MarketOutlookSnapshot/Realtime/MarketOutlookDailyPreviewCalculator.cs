using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;

/// <summary>Calculates a non-durable Daily preview from the prior close baseline plus one live ES trade.</summary>
public static class MarketOutlookDailyPreviewCalculator
{
    /// <summary>Returns a live EMA/BB preview without mutating either committed accumulator.</summary>
    public static bool TryCalculate(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        out FuturesEmaSignalReadModel ema,
        out FuturesBbSignalReadModel bb)
    {
        ema = default!;
        bb = default!;
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase)
            || trade.NormalizedTradeAction != NormalizedTradeAction.New
            || trade.LastPrice <= 0m
            || trade.LastSize == 0
            || trade.StreamEpochId == Guid.Empty
            || trade.TradeOrdinal <= 0
            || !RegimeDiscoverySignalCacheAdapter.TryGetLatestEsDailyBaseline(
                source.Price.ContractId,
                out var emaBaseline,
                out var bbBaseline,
                out var committedEma,
                out _))
            return false;

        var marketTime = trade.EventTimestamp.ToUniversalTime();
        var intervalEnd = marketTime > emaBaseline.LastIntervalEndUtc
            ? marketTime.AddTicks(1)
            : emaBaseline.LastIntervalEndUtc.AddTicks(1);
        var intervalStart = intervalEnd.AddDays(-1);
        var series = committedEma.Metadata.MarketSeriesIdentity;
        var observation = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series,
            ObservationId = FuturesTradeSessionBarId.Create(
                series,
                TimeFrameType.Daily,
                intervalEnd,
                trade.SourceSequence),
            ContractId = source.Price.ContractId,
            ValueDate = source.Price.ValueDate,
            TimeFrame = TimeFrameType.Daily,
            IntervalStartUtc = intervalStart,
            IntervalEndUtc = intervalEnd,
            Open = trade.LastPrice,
            High = trade.LastPrice,
            Low = trade.LastPrice,
            Close = trade.LastPrice,
            Volume = trade.LastSize,
            TradeCount = 1,
            PriceVolumeSum = trade.LastPrice * trade.LastSize,
            FirstSourceSequence = trade.SourceSequence,
            LastSourceSequence = trade.SourceSequence,
            FirstMarketEventUtc = marketTime,
            LastMarketEventUtc = marketTime,
            CalculatedAtUtc = DateTimeOffset.UtcNow,
            SchemaVersion = 1,
            CalculationVersion = "market-outlook-live-preview-v1",
            IsComplete = false,
            IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.ExactTrades,
            StreamEpochId = trade.StreamEpochId
        };
        var emaResult = FuturesEmaAccumulator.Apply(emaBaseline, observation);
        if (emaResult.Signal is not { } previewEma)
            return false;
        var bbResult = FuturesBbAccumulator.Apply(bbBaseline, observation, previewEma);
        if (bbResult.Signal is not { } previewBb)
            return false;
        ema = previewEma with
        {
            IsProvisional = true,
            BaselineValueDate = committedEma.Metadata.ValueDate,
            LivePriceAsOfUtc = marketTime
        };
        bb = previewBb with
        {
            IsProvisional = true,
            BaselineValueDate = committedEma.Metadata.ValueDate,
            LivePriceAsOfUtc = marketTime
        };
        return true;
    }
}
