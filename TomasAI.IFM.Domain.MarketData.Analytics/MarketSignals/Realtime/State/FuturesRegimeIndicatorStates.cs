using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;

/// <summary>Calculates one isolated RSI configuration from shared closed observations.</summary>
public sealed class FuturesRegimeRsiSignalState(int period, string configurationId)
{
    decimal? previousClose;
    decimal averageGain;
    decimal averageLoss;
    int sampleCount;
    double? previousRsi;
    FuturesAnalyticsObservationId lastObservationId;

    /// <summary>Gets the configured RSI period.</summary>
    public int Period { get; } = period > 0 ? period : throw new ArgumentOutOfRangeException(nameof(period));

    /// <summary>Gets the immutable configuration identity.</summary>
    public string ConfigurationId { get; } = !string.IsNullOrWhiteSpace(configurationId)
        ? configurationId
        : throw new ArgumentException("Configuration identity is required.", nameof(configurationId));

    /// <summary>Applies one unique shared observation.</summary>
    /// <param name="observation">Closed observation to apply.</param>
    /// <returns>The RSI projection for this observation.</returns>
    public FuturesRegimeRsiSignalReadModel Apply(FuturesAnalyticsObservationReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        EnsureUnique(lastObservationId, observation.ObservationId);
        var change = previousClose is null ? 0 : observation.Close - previousClose.Value;
        var gain = Math.Max(change, 0);
        var loss = Math.Max(-change, 0);
        sampleCount++;

        double? current = null;
        if (sampleCount <= Period)
        {
            averageGain += gain;
            averageLoss += loss;
            if (sampleCount == Period)
            {
                averageGain /= Period;
                averageLoss /= Period;
                current = CalculateRsi(averageGain, averageLoss);
            }
        }
        else
        {
            averageGain = ((averageGain * (Period - 1)) + gain) / Period;
            averageLoss = ((averageLoss * (Period - 1)) + loss) / Period;
            current = CalculateRsi(averageGain, averageLoss);
        }

        var prior = previousRsi;
        double? slope = current is not null && prior is not null ? current.Value - prior.Value : null;
        previousClose = observation.Close;
        if (current is not null) previousRsi = current;
        lastObservationId = observation.ObservationId;

        return new FuturesRegimeRsiSignalReadModel
        {
            Metadata = Metadata(observation, MarketAnalyticsSignalKind.Rsi, ConfigurationId, "rsi-wilder-v1"),
            Period = Period,
            Value = current,
            PreviousValue = prior,
            Slope = slope,
            IsWarm = current is not null && prior is not null
        };
    }

    static double CalculateRsi(decimal averageGainValue, decimal averageLossValue) =>
        averageLossValue == 0
            ? averageGainValue == 0 ? 50d : 100d
            : 100d - (100d / (1d + (double)(averageGainValue / averageLossValue)));

    internal static MarketAnalyticsSignalMetadata Metadata(
        FuturesAnalyticsObservationReadModel observation,
        MarketAnalyticsSignalKind kind,
        string configurationId,
        string calculationVersion) => new()
    {
        SignalKey = new(observation.MarketSeriesIdentity, kind, observation.TimeFrame, configurationId),
        ContractId = observation.ContractId,
        ValueDate = observation.ValueDate,
        ObservationId = observation.ObservationId,
        MarketDataAsOfUtc = observation.LastMarketEventUtc,
        CalculatedAtUtc = observation.CalculatedAtUtc,
        SourceSequence = observation.LastSourceSequence,
        SchemaVersion = 1,
        CalculationVersion = calculationVersion,
        CalculationMethod = observation.CalculationMethod,
        IsValid = observation.IsValid,
        ValidationIssues = observation.ValidationIssues
    };

    internal static void EnsureUnique(
        FuturesAnalyticsObservationId previous,
        FuturesAnalyticsObservationId current)
    {
        if (previous.Value != Guid.Empty && previous == current)
            throw new InvalidOperationException($"Observation {current} has already been applied.");
    }
}

/// <summary>Calculates EMA10/20/50/200 from a single shared observation lineage.</summary>
public sealed class FuturesEmaSignalRealtimeState
{
    const string ConfigurationId = "ema-10-20-50-200-v1";
    readonly PeriodEma ema10 = new(10);
    readonly PeriodEma ema20 = new(20);
    readonly PeriodEma ema50 = new(50);
    readonly PeriodEma ema200 = new(200);
    FuturesAnalyticsObservationId lastObservationId;

    /// <summary>Applies one unique shared observation and returns the complete EMA family.</summary>
    public FuturesEmaSignalReadModel Apply(FuturesAnalyticsObservationReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        FuturesRegimeRsiSignalState.EnsureUnique(lastObservationId, observation.ObservationId);
        var value10 = ema10.Apply(observation.Close);
        var value20 = ema20.Apply(observation.Close);
        var value50 = ema50.Apply(observation.Close);
        var value200 = ema200.Apply(observation.Close);
        lastObservationId = observation.ObservationId;
        return new()
        {
            Metadata = FuturesRegimeRsiSignalState.Metadata(
                observation, MarketAnalyticsSignalKind.Ema, ConfigurationId, "ema-sma-seed-v1"),
            Price = observation.Close,
            Ema10 = value10.Current,
            PreviousEma10 = value10.Previous,
            Ema10Slope = value10.Slope,
            Ema20 = value20.Current,
            PreviousEma20 = value20.Previous,
            Ema20Slope = value20.Slope,
            Ema50 = value50.Current,
            PreviousEma50 = value50.Previous,
            Ema50Slope = value50.Slope,
            Ema200 = value200.Current,
            PreviousEma200 = value200.Previous,
            Ema200Slope = value200.Slope,
            IsWarm = value200.Current is not null && value200.Previous is not null
        };
    }

    sealed class PeriodEma(int period)
    {
        readonly decimal multiplier = 2m / (period + 1m);
        decimal seedSum;
        int count;
        decimal? current;

        public EmaValue Apply(decimal close)
        {
            var previous = current;
            count++;
            if (count <= period)
            {
                seedSum += close;
                if (count == period) current = seedSum / period;
            }
            else
            {
                current = ((close - current!.Value) * multiplier) + current.Value;
            }
            return new(current, previous, current is not null && previous is not null ? current - previous : null);
        }
    }

    readonly record struct EmaValue(decimal? Current, decimal? Previous, decimal? Slope);
}

/// <summary>Calculates EMA-centered BB10/20 and a prior-only BB20 width baseline.</summary>
public sealed class FuturesBollingerBandSignalRealtimeState
{
    const string ConfigurationId = "bb-10-20-ema-center-population-v1";
    readonly Queue<decimal> closes = new();
    readonly Queue<decimal> completedWidths20 = new();
    FuturesAnalyticsObservationId lastObservationId;

    /// <summary>Tries to apply a same-observation EMA/close pair.</summary>
    /// <param name="observation">Source closed observation.</param>
    /// <param name="ema">EMA family calculated from that exact observation.</param>
    /// <param name="signal">Receives the Bollinger signal when identities match.</param>
    /// <returns><see langword="false"/> when the EMA lineage does not match.</returns>
    public bool TryApply(
        FuturesAnalyticsObservationReadModel observation,
        FuturesEmaSignalReadModel ema,
        out FuturesBollingerBandSignalReadModel signal)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(ema);
        if (ema.Metadata.ObservationId != observation.ObservationId)
        {
            signal = default!;
            return false;
        }
        FuturesRegimeRsiSignalState.EnsureUnique(lastObservationId, observation.ObservationId);
        closes.Enqueue(observation.Close);
        while (closes.Count > 20) closes.Dequeue();

        decimal? sd10 = closes.Count >= 10 ? PopulationStandardDeviation(closes.TakeLast(10)) : null;
        decimal? sd20 = closes.Count >= 20 ? PopulationStandardDeviation(closes) : null;
        decimal? width10 = sd10 is not null && ema.Ema10 is not null ? 4m * sd10 : null;
        decimal? width20 = sd20 is not null && ema.Ema20 is not null ? 4m * sd20 : null;
        decimal? baseline = completedWidths20.Count == 20 ? completedWidths20.Average() : null;
        signal = new()
        {
            Metadata = FuturesRegimeRsiSignalState.Metadata(
                observation, MarketAnalyticsSignalKind.BollingerBand, ConfigurationId, "bb-ema-center-population-v1"),
            Price = observation.Close,
            Ema10Center = ema.Ema10,
            StandardDeviation10 = sd10,
            Upper10 = ema.Ema10 + (2m * sd10),
            Lower10 = ema.Ema10 - (2m * sd10),
            Width10 = width10,
            Position10 = Position(observation.Close, ema.Ema10, sd10),
            Ema20Center = ema.Ema20,
            StandardDeviation20 = sd20,
            Upper20 = ema.Ema20 + (2m * sd20),
            Lower20 = ema.Ema20 - (2m * sd20),
            Width20 = width20,
            Position20 = Position(observation.Close, ema.Ema20, sd20),
            Width20Baseline = baseline,
            Width20Ratio = width20 is > 0 && baseline is > 0 ? width20 / baseline : null,
            IsWarm = width20 is > 0 && baseline is > 0
        };
        if (width20 is > 0)
        {
            completedWidths20.Enqueue(width20.Value);
            while (completedWidths20.Count > 20) completedWidths20.Dequeue();
        }
        lastObservationId = observation.ObservationId;
        return true;
    }

    static decimal? Position(decimal close, decimal? center, decimal? standardDeviation)
    {
        if (center is null || standardDeviation is not > 0) return null;
        var lower = center.Value - (2m * standardDeviation.Value);
        return (close - lower) / (4m * standardDeviation.Value);
    }

    static decimal PopulationStandardDeviation(IEnumerable<decimal> values)
    {
        var data = values.ToArray();
        var mean = data.Average();
        var variance = data.Average(value => (value - mean) * (value - mean));
        return (decimal)Math.Sqrt((double)variance);
    }
}

/// <summary>Calculates Wilder ATR14 and a prior-only 20-value volatility baseline.</summary>
public sealed class FuturesAtrVolatilitySignalRealtimeState
{
    const int Period = 14;
    const string ConfigurationId = "atr-14-baseline-20-v1";
    readonly Queue<decimal> seedTrueRanges = new();
    readonly Queue<decimal> completedAtrValues = new();
    decimal? previousClose;
    decimal? currentAtr;
    FuturesAnalyticsObservationId lastObservationId;

    /// <summary>Applies one unique shared OHLC observation.</summary>
    public FuturesAtrVolatilitySignalReadModel Apply(FuturesAnalyticsObservationReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        FuturesRegimeRsiSignalState.EnsureUnique(lastObservationId, observation.ObservationId);
        var trueRange = previousClose is null
            ? observation.High - observation.Low
            : Math.Max(observation.High - observation.Low,
                Math.Max(Math.Abs(observation.High - previousClose.Value),
                    Math.Abs(observation.Low - previousClose.Value)));
        var prior = currentAtr;
        decimal? baseline = completedAtrValues.Count == 20 ? completedAtrValues.Average() : null;
        if (currentAtr is null)
        {
            seedTrueRanges.Enqueue(trueRange);
            if (seedTrueRanges.Count == Period) currentAtr = seedTrueRanges.Average();
        }
        else
        {
            currentAtr = ((currentAtr.Value * (Period - 1)) + trueRange) / Period;
        }
        var ratio = currentAtr is not null && baseline is > 0 ? currentAtr / baseline : null;
        var result = new FuturesAtrVolatilitySignalReadModel
        {
            Metadata = FuturesRegimeRsiSignalState.Metadata(
                observation, MarketAnalyticsSignalKind.Atr, ConfigurationId, "atr-wilder-baseline-v1"),
            TrueRange = trueRange,
            Atr14 = currentAtr,
            PreviousAtr14 = prior,
            Atr14Baseline = baseline,
            Atr14Ratio = ratio,
            IsWarm = currentAtr is not null && prior is not null && baseline is > 0
        };
        if (currentAtr is not null)
        {
            completedAtrValues.Enqueue(currentAtr.Value);
            while (completedAtrValues.Count > 20) completedAtrValues.Dequeue();
        }
        previousClose = observation.Close;
        lastObservationId = observation.ObservationId;
        return result;
    }
}

/// <summary>
/// Owns isolated per-series state and calculates every MDSI-7 through MDSI-10 output in dependency order.
/// </summary>
public sealed class FuturesRegimeIndicatorPipelineRealtimeState
{
    readonly Dictionary<FuturesAnalyticsObservationEntityId, PipelineState> states = [];

    /// <summary>Calculates RSI13, RSI14, EMA, Bollinger Bands, and ATR for one observation.</summary>
    public FuturesRegimeIndicatorSnapshot Apply(FuturesAnalyticsObservationReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var entityId = new FuturesAnalyticsObservationEntityId(
            observation.MarketSeriesIdentity,
            observation.TimeFrame);
        if (!states.TryGetValue(entityId, out var state))
        {
            state = new();
            states.Add(entityId, state);
        }
        var ema = state.Ema.Apply(observation);
        if (!state.BollingerBand.TryApply(observation, ema, out var bollingerBand))
            throw new InvalidOperationException("EMA and Bollinger observation identities must match.");
        return new()
        {
            Observation = observation,
            Rsi13 = state.Rsi13.Apply(observation),
            Rsi14 = state.Rsi14.Apply(observation),
            Ema = ema,
            BollingerBand = bollingerBand,
            AtrVolatility = state.Atr.Apply(observation)
        };
    }

    sealed class PipelineState
    {
        public FuturesRegimeRsiSignalState Rsi13 { get; } =
            new(13, FuturesRsiConfigurations.TdiRsi13);
        public FuturesRegimeRsiSignalState Rsi14 { get; } =
            new(14, FuturesRsiConfigurations.RegimeRsi14);
        public FuturesEmaSignalRealtimeState Ema { get; } = new();
        public FuturesBollingerBandSignalRealtimeState BollingerBand { get; } = new();
        public FuturesAtrVolatilitySignalRealtimeState Atr { get; } = new();
    }
}
