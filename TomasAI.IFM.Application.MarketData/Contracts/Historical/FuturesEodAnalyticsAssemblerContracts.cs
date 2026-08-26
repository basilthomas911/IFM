using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Identifies an independently persisted Analytics signal family.</summary>
public enum MarketAnalyticsSignalFamily : byte
{
    /// <summary>Exponential moving averages.</summary>
    Ema = 1,
    /// <summary>Bollinger bands.</summary>
    BollingerBand = 2,
    /// <summary>ATR volatility context.</summary>
    AtrVolatility = 3,
    /// <summary>Market-structure context.</summary>
    MarketStructure = 4,
    /// <summary>VX front/back term structure.</summary>
    VxTermStructure = 5
}

/// <summary>Provides exact common metadata plus an opaque typed-signal payload.</summary>
public sealed record MarketAnalyticsSignalResult(
    MarketAnalyticsSignalFamily Family,
    MarketAnalyticsSignalMetadata Metadata,
    object Value);

/// <summary>Reads already-calculated signals; implementations never derive a value from raw EOD.</summary>
public interface IHistoricalAnalyticsSignalReader
{
    /// <summary>Gets a signal whose ObservationId exactly matches the raw session.</summary>
    ValueTask<MarketAnalyticsSignalResult?> GetExactAsync(
        MarketAnalyticsSignalFamily family,
        MarketSeriesIdentity seriesIdentity,
        TimeFrameType timeFrame,
        FuturesTradeSessionBarId observationId,
        CancellationToken cancellationToken);
}

/// <summary>Reports signals as unavailable until their MDSI migration gate registers a typed reader.</summary>
public sealed class NullHistoricalAnalyticsSignalReader : IHistoricalAnalyticsSignalReader
{
    /// <inheritdoc />
    public ValueTask<MarketAnalyticsSignalResult?> GetExactAsync(
        MarketAnalyticsSignalFamily family,
        MarketSeriesIdentity seriesIdentity,
        TimeFrameType timeFrame,
        FuturesTradeSessionBarId observationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<MarketAnalyticsSignalResult?>(null);
    }
}

/// <summary>Combines raw EOD facts with exact independently calculated Analytics signals.</summary>
public sealed record FuturesEodAnalyticsAssembly(
    FuturesEodObservationReadModel Raw,
    IReadOnlyDictionary<MarketAnalyticsSignalFamily, MarketAnalyticsSignalResult> Signals,
    IReadOnlyList<MarketAnalyticsSignalFamily> MissingSignals);

/// <summary>Defines the compatibility query boundary used while UI/API consumers leave the derived EOD model.</summary>
public interface IFuturesEodAnalyticsAssembler
{
    /// <summary>Loads raw EOD and exact same-observation Analytics signals without recalculation.</summary>
    ValueTask<FuturesEodAnalyticsAssembly?> AssembleAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        IReadOnlyCollection<MarketAnalyticsSignalFamily> requestedSignals,
        CancellationToken cancellationToken);
}
