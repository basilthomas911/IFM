using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using System.Collections.Concurrent;

namespace TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

/// <summary>Normalizes existing analytics read models into the unified Regime Discovery latest-signal cache.</summary>
public static class RegimeDiscoverySignalCacheAdapter
{
    static readonly RegimeDiscoveryMarketSignalSnapshotProvider Cache = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), decimal> LatestAtr = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), decimal> LatestEma20 = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), FuturesEmaSignalReadModel> LatestEmaSignals = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), FuturesBbSignalReadModel> LatestBbSignals = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), FuturesEmaAccumulatorCheckpoint> LatestEmaCheckpoints = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), FuturesBbAccumulatorCheckpoint> LatestBbCheckpoints = new();
    static readonly ConcurrentDictionary<(string ContractId, TimeFrameType TimeFrame), Queue<FuturesTradeSessionBarReadModel>> Bars = new();

    /// <summary>Publishes the EMA family and current price using common observation provenance.</summary>
    public static void Publish(FuturesEmaSignalReadModel signal)
    {
        LatestEmaSignals[(signal.Metadata.ContractId, signal.Metadata.TimeFrame)] = signal;
        Publish(signal.Metadata, RegimeDiscoverySignalMetric.CurrentPrice, signal.Price, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema20, signal.Ema20, signal.IsWarm);
        if (signal.Ema20 is { } ema20)
            LatestEma20[(signal.Metadata.ContractId, signal.Metadata.TimeFrame)] = ema20;
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema50, signal.Ema50, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema200, signal.Ema200, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema20Slope, signal.Ema20Slope, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema50Slope, signal.Ema50Slope, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.Ema200Slope, signal.Ema200Slope, signal.IsWarm);
    }

    /// <summary>Publishes a committed EMA signal together with its immutable Daily baseline.</summary>
    public static void Publish(FuturesEmaSignalReadModel signal, FuturesEmaAccumulatorCheckpoint checkpoint)
    {
        Publish(signal);
        if (!signal.IsProvisional)
            LatestEmaCheckpoints[(signal.Metadata.ContractId, signal.Metadata.TimeFrame)] = checkpoint;
    }

    /// <summary>Publishes Bollinger width, ratio, position, and price interaction inputs.</summary>
    public static void Publish(FuturesBbSignalReadModel signal)
    {
        LatestBbSignals[(signal.Metadata.ContractId, signal.Metadata.TimeFrame)] = signal;
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.BollingerWidth, signal.Width20, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.BollingerWidthRatio, signal.Width20Ratio, signal.IsWarm);
        PublishNullable(signal.Metadata, RegimeDiscoverySignalMetric.BollingerPosition, signal.Position20, signal.IsWarm);
    }

    /// <summary>Publishes a committed Bollinger signal together with its immutable Daily baseline.</summary>
    public static void Publish(FuturesBbSignalReadModel signal, FuturesBbAccumulatorCheckpoint checkpoint)
    {
        Publish(signal);
        if (!signal.IsProvisional)
            LatestBbCheckpoints[(signal.Metadata.ContractId, signal.Metadata.TimeFrame)] = checkpoint;
    }

    /// <summary>
    /// Publishes one immutable completed-session baseline under the active domain contract alias.
    /// Historical observations can carry a provider instrument id while live prices carry the
    /// canonical contract id; the preview calculator must resolve both to the same baseline.
    /// </summary>
    public static void PublishDailyBaseline(
        string activeContractId,
        FuturesEmaSignalReadModel emaSignal,
        FuturesEmaAccumulatorCheckpoint emaCheckpoint,
        FuturesBbSignalReadModel bbSignal,
        FuturesBbAccumulatorCheckpoint bbCheckpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeContractId);
        ArgumentNullException.ThrowIfNull(emaSignal);
        ArgumentNullException.ThrowIfNull(emaCheckpoint);
        ArgumentNullException.ThrowIfNull(bbSignal);
        ArgumentNullException.ThrowIfNull(bbCheckpoint);

        Publish(emaSignal, emaCheckpoint);
        Publish(bbSignal, bbCheckpoint);
        var alias = (activeContractId, TimeFrameType.Daily);
        LatestEmaSignals[alias] = emaSignal;
        LatestBbSignals[alias] = bbSignal;
        LatestEmaCheckpoints[alias] = emaCheckpoint;
        LatestBbCheckpoints[alias] = bbCheckpoint;
    }

    /// <summary>Gets the newest warm committed ES Daily baseline for live preview calculation.</summary>
    public static bool TryGetLatestEsDailyBaseline(
        string contractId,
        out FuturesEmaAccumulatorCheckpoint ema,
        out FuturesBbAccumulatorCheckpoint bb,
        out FuturesEmaSignalReadModel committedEma,
        out FuturesBbSignalReadModel committedBb)
    {
        var exact = (contractId, TimeFrameType.Daily);
        if (LatestEmaCheckpoints.TryGetValue(exact, out ema!)
            && LatestBbCheckpoints.TryGetValue(exact, out bb!)
            && LatestEmaSignals.TryGetValue(exact, out committedEma!)
            && LatestBbSignals.TryGetValue(exact, out committedBb!))
            return true;

        var candidate = LatestEmaSignals
            .Where(static pair => pair.Key.TimeFrame == TimeFrameType.Daily)
            .Where(static pair => pair.Key.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase))
            .Where(pair => LatestBbSignals.ContainsKey(pair.Key)
                && LatestEmaCheckpoints.ContainsKey(pair.Key)
                && LatestBbCheckpoints.ContainsKey(pair.Key))
            .OrderByDescending(static pair => pair.Value.Metadata.ValueDate)
            .ThenByDescending(static pair => pair.Value.Metadata.SourceSequence)
            .FirstOrDefault();
        if (candidate.Value is null)
        {
            ema = default!;
            bb = default!;
            committedEma = default!;
            committedBb = default!;
            return false;
        }
        ema = LatestEmaCheckpoints[candidate.Key];
        bb = LatestBbCheckpoints[candidate.Key];
        committedEma = candidate.Value;
        committedBb = LatestBbSignals[candidate.Key];
        return true;
    }

    /// <summary>Gets the latest typed EMA family for one exact contract and timeframe.</summary>
    public static bool TryGetLatestEma(
        string contractId,
        TimeFrameType timeFrame,
        out FuturesEmaSignalReadModel signal) =>
        LatestEmaSignals.TryGetValue((contractId, timeFrame), out signal!);

    /// <summary>Gets the latest typed Bollinger family for one exact contract and timeframe.</summary>
    public static bool TryGetLatestBb(
        string contractId,
        TimeFrameType timeFrame,
        out FuturesBbSignalReadModel signal) =>
        LatestBbSignals.TryGetValue((contractId, timeFrame), out signal!);

    /// <summary>Publishes ADX14, +DI14, and -DI14.</summary>
    public static void Publish(FuturesAdxSignalReadModel signal)
    {
        var metadata = Metadata(signal.Metadata, signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.Timestamp, "adx-v1");
        Publish(metadata, RegimeDiscoverySignalMetric.Adx14, (decimal)signal.AdxValue, true);
        Publish(metadata, RegimeDiscoverySignalMetric.PlusDi14, (decimal)signal.PlusDI, true);
        Publish(metadata, RegimeDiscoverySignalMetric.MinusDi14, (decimal)signal.MinusDI, true);
    }

    /// <summary>Publishes Wilder ATR14 and its warm baseline ratio.</summary>
    public static void Publish(FuturesAtrSignalReadModel signal)
    {
        var metadata = Metadata(signal.Metadata, signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.Timestamp, "atr-v1");
        Publish(metadata, RegimeDiscoverySignalMetric.Atr14, (decimal)signal.AtrValue, signal.IsWarm);
        LatestAtr[(signal.ContractId, signal.TimePeriod)] = (decimal)signal.AtrValue;
        PublishNullable(metadata, RegimeDiscoverySignalMetric.AtrBaselineRatio,
            signal.AtrRatio is { } ratio ? (decimal)ratio : null, signal.IsWarm);
    }

    /// <summary>Publishes RSI14 and its slope.</summary>
    public static void Publish(FuturesRsiSignalReadModel signal)
    {
        if (signal is not { IsWarm: true, RSI: >= 0d }
            || signal.Metadata is { IsValid: false })
            return;
        var metadata = Metadata(signal.Metadata, signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.Timestamp, "rsi-v1");
        Publish(metadata, RegimeDiscoverySignalMetric.Rsi14, (decimal)signal.RSI, true);
        Publish(metadata, RegimeDiscoverySignalMetric.Rsi14Slope, (decimal)signal.RSISlope, true);
    }

    /// <summary>Publishes the conventional MACD histogram.</summary>
    public static void Publish(FuturesMacdSignalReadModel signal)
    {
        var metadata = Metadata(signal.Metadata, signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.Timestamp, "macd-v1");
        Publish(metadata, RegimeDiscoverySignalMetric.MacdHistogram,
            (decimal)(signal.MacdLine - signal.SignalLine), true);
    }

    /// <summary>Publishes current ITI direction, threshold progress, reversal progress, and price.</summary>
    public static void Publish(FuturesItiSignalV2ReadModel signal, long sourceSequence,
        DateTime calculatedAtUtc, decimal vixLevel = 0m)
    {
        var metadata = Synthetic(signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.IntrinsicTime, calculatedAtUtc, sourceSequence, MarketAnalyticsSignalKind.Iti, "iti-v1");
        Publish(metadata, RegimeDiscoverySignalMetric.CurrentPrice, (decimal)signal.IntrinsicPrice, true);
        if (vixLevel > 0)
            Publish(metadata, RegimeDiscoverySignalMetric.VxFrontLevel, vixLevel, true);
        Publish(metadata, RegimeDiscoverySignalMetric.ItiDirection,
            signal.IntrinsicTimeTrend.ToString().Contains("Up", StringComparison.OrdinalIgnoreCase) ? 1m : -1m, true);
        Publish(metadata, RegimeDiscoverySignalMetric.ItiBandLevel, (decimal)signal.BandLevel, true);
        Publish(metadata, RegimeDiscoverySignalMetric.ItiReversalLevel, (decimal)signal.ReversalLevel, true);
    }

    /// <summary>Publishes optional signed TDI confirmation evidence.</summary>
    public static void Publish(FuturesTdiSignalReadModel signal)
    {
        var direction = signal.TDI switch
        {
            FuturesTrendDirectionType.UpTrending => 1m,
            FuturesTrendDirectionType.DownTrending => -1m,
            _ => 0m
        };
        var strength = signal.TDIStrength switch
        {
            FuturesTrendDirectionStrengthType.High => 1m,
            FuturesTrendDirectionStrengthType.Medium => 0.66m,
            _ => 0.33m
        };
        var metadata = Synthetic(signal.ContractId, signal.TimePeriod, signal.ValueDate,
            signal.SourceEventTimestamp == default
                ? signal.ValueDate.ToDateTime(signal.Timestamp, DateTimeKind.Utc)
                : DateTime.SpecifyKind(signal.SourceEventTimestamp, DateTimeKind.Utc),
            signal.SourceEventTimestamp == default
                ? signal.ValueDate.ToDateTime(signal.Timestamp, DateTimeKind.Utc)
                : DateTime.SpecifyKind(signal.SourceEventTimestamp, DateTimeKind.Utc),
            signal.SourceSequence, MarketAnalyticsSignalKind.Tdi, $"{RegimeDiscoverySignalMetric.Tdi}.v1");
        Publish(metadata, RegimeDiscoverySignalMetric.Tdi, direction * strength, true);
    }

    /// <summary>Publishes rolling range, high/low, breakout distance, and EMA interaction from a closed OHLCV bar.</summary>
    public static void Publish(FuturesTradeSessionBarReadModel bar)
    {
        var key = (bar.ContractId, bar.TimeFrame);
        var queue = Bars.GetOrAdd(key, static _ => new Queue<FuturesTradeSessionBarReadModel>(20));
        lock (queue)
        {
            if (queue.Count >= 20 && LatestAtr.TryGetValue(key, out var atr) && atr > 0)
            {
                var high = queue.Max(value => value.High);
                var low = queue.Min(value => value.Low);
                var metadata = Synthetic(bar.ContractId, bar.TimeFrame, bar.ValueDate,
                    bar.LastMarketEventUtc.UtcDateTime, bar.CalculatedAtUtc.UtcDateTime,
                    bar.LastSourceSequence, MarketAnalyticsSignalKind.MarketStructure,
                    "market-structure-v1");
                Publish(metadata, RegimeDiscoverySignalMetric.RollingHigh20, high, true);
                Publish(metadata, RegimeDiscoverySignalMetric.RollingLow20, low, true);
                Publish(metadata, RegimeDiscoverySignalMetric.AtrNormalizedRange,
                    (bar.High - bar.Low) / atr, true);
                var breakout = bar.Close > high ? (bar.Close - high) / atr
                    : bar.Close < low ? (bar.Close - low) / atr : 0m;
                Publish(metadata, RegimeDiscoverySignalMetric.BreakoutDistanceAtr, breakout, true);
                if (LatestEma20.TryGetValue(key, out var ema20))
                    Publish(metadata, RegimeDiscoverySignalMetric.Ema20Interaction,
                        (bar.Close - ema20) / atr, true);
            }
            queue.Enqueue(bar);
            while (queue.Count > 20)
                queue.Dequeue();
        }
    }

    /// <summary>Publishes the VX front/second ratio.</summary>
    public static void Publish(FuturesVxTermStructureSignalReadModel signal)
    {
        var metadata = Synthetic(signal.FrontVxContractId, TimeFrameType.Daily, signal.ValueDate,
            signal.FrontSourceTimestampUtc.UtcDateTime, signal.CalculatedAtUtc.UtcDateTime,
            Math.Max(signal.FrontSourceSequence, signal.BackSourceSequence),
            MarketAnalyticsSignalKind.VxTermStructure, signal.ConfigurationId);
        Publish(metadata, RegimeDiscoverySignalMetric.VxFrontSecondRatio,
            signal.FrontBackRatio, signal.IsWarm && signal.IsValid);
    }

    static void PublishNullable(MarketAnalyticsSignalMetadata metadata,
        RegimeDiscoverySignalMetric metric, decimal? value, bool warm)
    {
        if (value is { } present)
            Publish(metadata, metric, present, warm);
    }

    static void Publish(MarketAnalyticsSignalMetadata metadata,
        RegimeDiscoverySignalMetric metric, decimal value, bool warm)
    {
        var key = new MarketAnalyticsSignalKey(metadata.SignalKey.MarketSeriesIdentity, Kind(metric),
            metadata.TimeFrame, $"{metric}.v1");
        Cache.Upsert(new RegimeDiscoverySignalObservation
        {
            Metric = metric,
            SignalKey = key,
            Value = value,
            MarketDataAsOfUtc = metadata.MarketDataAsOfUtc.UtcDateTime,
            CalculatedAtUtc = metadata.CalculatedAtUtc.UtcDateTime,
            SourceSequence = metadata.SourceSequence,
            SchemaVersion = metadata.SchemaVersion == 0 ? (ushort)1 : metadata.SchemaVersion,
            CalculationVersion = "1",
            IsWarm = warm,
            IsValid = metadata.IsValid,
            Availability = RegimeDiscoverySignalAvailability.Available,
            SignalIdentity = $"{key.MarketSeriesIdentity.Format()}.{metric}.{key.TimeFrame}"
        });
    }

    static MarketAnalyticsSignalMetadata Metadata(MarketAnalyticsSignalMetadata? metadata,
        string contractId, TimeFrameType timeFrame, DateOnly valueDate, TimeOnly timestamp,
        string configurationId) => metadata ?? Synthetic(contractId, timeFrame, valueDate,
            valueDate.ToDateTime(timestamp, DateTimeKind.Utc), DateTime.UtcNow, 1,
            MarketAnalyticsSignalKind.MarketStructure, configurationId);

    static MarketAnalyticsSignalMetadata Synthetic(string contractId, TimeFrameType timeFrame,
        DateOnly valueDate, DateTime marketDataAtUtc, DateTime calculatedAtUtc, long sequence,
        MarketAnalyticsSignalKind kind, string configurationId) => new()
        {
            SignalKey = new MarketAnalyticsSignalKey(MarketSeriesIdentity.ForContract(contractId), kind,
                timeFrame, configurationId),
            ContractId = contractId,
            ValueDate = valueDate,
            MarketDataAsOfUtc = new DateTimeOffset(DateTime.SpecifyKind(marketDataAtUtc, DateTimeKind.Utc)),
            CalculatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(calculatedAtUtc, DateTimeKind.Utc)),
            SourceSequence = sequence,
            SchemaVersion = 1,
            CalculationVersion = "1",
            IsValid = true
        };

    static MarketAnalyticsSignalKind Kind(RegimeDiscoverySignalMetric metric) => metric switch
    {
        RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or
            RegimeDiscoverySignalMetric.Ema200 or RegimeDiscoverySignalMetric.Ema20Slope or
            RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope or
            RegimeDiscoverySignalMetric.Ema20Interaction => MarketAnalyticsSignalKind.Ema,
        RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => MarketAnalyticsSignalKind.Rsi,
        RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14 or
            RegimeDiscoverySignalMetric.MinusDi14 => MarketAnalyticsSignalKind.Adx,
        RegimeDiscoverySignalMetric.MacdHistogram => MarketAnalyticsSignalKind.Macd,
        RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio or
            RegimeDiscoverySignalMetric.AtrNormalizedRange => MarketAnalyticsSignalKind.Atr,
        RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio or
            RegimeDiscoverySignalMetric.BollingerPosition => MarketAnalyticsSignalKind.BollingerBand,
        RegimeDiscoverySignalMetric.VxFrontSecondRatio or RegimeDiscoverySignalMetric.VixLevel or
            RegimeDiscoverySignalMetric.VxFrontLevel => MarketAnalyticsSignalKind.VxTermStructure,
        RegimeDiscoverySignalMetric.ItiDirection or RegimeDiscoverySignalMetric.ItiBandLevel or
            RegimeDiscoverySignalMetric.ItiReversalLevel or RegimeDiscoverySignalMetric.CurrentPrice => MarketAnalyticsSignalKind.Iti,
        RegimeDiscoverySignalMetric.Tdi => MarketAnalyticsSignalKind.Tdi,
        _ => MarketAnalyticsSignalKind.MarketStructure
    };
}
