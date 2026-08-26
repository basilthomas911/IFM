using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;

/// <summary>Defines the immutable RSI configurations used by TDI and Regime Discovery.</summary>
public static class FuturesRsiConfigurations
{
    /// <summary>Gets the RSI13 configuration reserved for the existing TDI pipeline.</summary>
    public const string TdiRsi13 = "rsi-13-tdi-v1";

    /// <summary>Gets the independent RSI14 configuration used by Regime Discovery.</summary>
    public const string RegimeRsi14 = "rsi-14-regime-v1";
}

/// <summary>Represents an RSI value with explicit warm-up and optional slope semantics.</summary>
[MessagePackObject]
public sealed record FuturesRegimeRsiSignalReadModel
{
    /// <summary>Gets common identity and observation provenance.</summary>
    [Key(0)] public MarketAnalyticsSignalMetadata Metadata { get; init; } = new();

    /// <summary>Gets the configured RSI period.</summary>
    [Key(1)] public int Period { get; init; }

    /// <summary>Gets the current RSI value when seeded.</summary>
    [Key(2)] public double? Value { get; init; }

    /// <summary>Gets the prior RSI value when available.</summary>
    [Key(3)] public double? PreviousValue { get; init; }

    /// <summary>Gets current minus prior RSI; missing means the signal is not slope-warm.</summary>
    [Key(4)] public double? Slope { get; init; }

    /// <summary>Gets whether both the period value and its prior value are available.</summary>
    [Key(5)] public bool IsWarm { get; init; }
}

/// <summary>Represents EMA10/20/50/200 calculated from one shared close lineage.</summary>
[MessagePackObject]
public sealed record FuturesEmaSignalReadModel
{
    /// <summary>Gets common identity and observation provenance.</summary>
    [Key(0)] public MarketAnalyticsSignalMetadata Metadata { get; init; } = new();

    /// <summary>Gets the source close.</summary>
    [Key(1)] public decimal Price { get; init; }

    /// <summary>Gets the current EMA10.</summary>
    [Key(2)] public decimal? Ema10 { get; init; }

    /// <summary>Gets the prior EMA10.</summary>
    [Key(3)] public decimal? PreviousEma10 { get; init; }

    /// <summary>Gets current minus prior EMA10.</summary>
    [Key(4)] public decimal? Ema10Slope { get; init; }

    /// <summary>Gets the current EMA20.</summary>
    [Key(5)] public decimal? Ema20 { get; init; }

    /// <summary>Gets the prior EMA20.</summary>
    [Key(6)] public decimal? PreviousEma20 { get; init; }

    /// <summary>Gets current minus prior EMA20.</summary>
    [Key(7)] public decimal? Ema20Slope { get; init; }

    /// <summary>Gets the current EMA50.</summary>
    [Key(8)] public decimal? Ema50 { get; init; }

    /// <summary>Gets the prior EMA50.</summary>
    [Key(9)] public decimal? PreviousEma50 { get; init; }

    /// <summary>Gets current minus prior EMA50.</summary>
    [Key(10)] public decimal? Ema50Slope { get; init; }

    /// <summary>Gets the current EMA200.</summary>
    [Key(11)] public decimal? Ema200 { get; init; }

    /// <summary>Gets the prior EMA200.</summary>
    [Key(12)] public decimal? PreviousEma200 { get; init; }

    /// <summary>Gets current minus prior EMA200.</summary>
    [Key(13)] public decimal? Ema200Slope { get; init; }

    /// <summary>Gets whether EMA200 and its prior value are available.</summary>
    [Key(14)] public bool IsWarm { get; init; }
}

/// <summary>Represents EMA-centered Bollinger Bands for 10- and 20-observation windows.</summary>
[MessagePackObject]
public sealed record FuturesBollingerBandSignalReadModel
{
    /// <summary>Gets common identity and observation provenance.</summary>
    [Key(0)] public MarketAnalyticsSignalMetadata Metadata { get; init; } = new();

    /// <summary>Gets the source close.</summary>
    [Key(1)] public decimal Price { get; init; }

    /// <summary>Gets the EMA10 centerline.</summary>
    [Key(2)] public decimal? Ema10Center { get; init; }

    /// <summary>Gets the population standard deviation for the 10-observation window.</summary>
    [Key(3)] public decimal? StandardDeviation10 { get; init; }

    /// <summary>Gets the upper BB10 value.</summary>
    [Key(4)] public decimal? Upper10 { get; init; }

    /// <summary>Gets the lower BB10 value.</summary>
    [Key(5)] public decimal? Lower10 { get; init; }

    /// <summary>Gets the BB10 width.</summary>
    [Key(6)] public decimal? Width10 { get; init; }

    /// <summary>Gets close position within BB10.</summary>
    [Key(7)] public decimal? Position10 { get; init; }

    /// <summary>Gets the EMA20 centerline.</summary>
    [Key(8)] public decimal? Ema20Center { get; init; }

    /// <summary>Gets the population standard deviation for the 20-observation window.</summary>
    [Key(9)] public decimal? StandardDeviation20 { get; init; }

    /// <summary>Gets the upper BB20 value.</summary>
    [Key(10)] public decimal? Upper20 { get; init; }

    /// <summary>Gets the lower BB20 value.</summary>
    [Key(11)] public decimal? Lower20 { get; init; }

    /// <summary>Gets the BB20 width.</summary>
    [Key(12)] public decimal? Width20 { get; init; }

    /// <summary>Gets close position within BB20.</summary>
    [Key(13)] public decimal? Position20 { get; init; }

    /// <summary>Gets the mean of the prior 20 completed BB20 widths.</summary>
    [Key(14)] public decimal? Width20Baseline { get; init; }

    /// <summary>Gets current BB20 width divided by its positive baseline.</summary>
    [Key(15)] public decimal? Width20Ratio { get; init; }

    /// <summary>Gets whether BB20 and its prior-width baseline are available.</summary>
    [Key(16)] public bool IsWarm { get; init; }
}
