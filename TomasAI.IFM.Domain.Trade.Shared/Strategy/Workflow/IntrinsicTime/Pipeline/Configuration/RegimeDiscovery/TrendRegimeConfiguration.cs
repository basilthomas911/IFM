using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures the deterministic Trend calculation.</summary>
[MessagePackObject]
public sealed record TrendRegimeConfiguration
{
    /// <summary>Gets the EMA alignment weight.</summary>
    [Key(0)] public decimal EmaAlignmentWeight { get; init; } = 0.25m;
    /// <summary>Gets the EMA slope weight.</summary>
    [Key(1)] public decimal EmaSlopeWeight { get; init; } = 0.15m;
    /// <summary>Gets the RSI weight.</summary>
    [Key(2)] public decimal RsiWeight { get; init; } = 0.15m;
    /// <summary>Gets the ADX weight.</summary>
    [Key(3)] public decimal AdxWeight { get; init; } = 0.20m;
    /// <summary>Gets the MACD weight.</summary>
    [Key(4)] public decimal MacdWeight { get; init; } = 0.15m;
    /// <summary>Gets the ITI weight.</summary>
    [Key(5)] public decimal ItiWeight { get; init; } = 0.10m;
    /// <summary>Gets the positive ATR-normalized EMA slope scale.</summary>
    [Key(6)] public decimal EmaSlopeScale { get; init; } = 0.10m;
    /// <summary>Gets the positive RSI slope scale.</summary>
    [Key(7)] public decimal RsiSlopeScale { get; init; } = 5m;
    /// <summary>Gets the positive MACD-to-ATR scale.</summary>
    [Key(8)] public decimal MacdAtrScale { get; init; } = 0.25m;
    /// <summary>Gets the absolute score at which direction becomes non-neutral.</summary>
    [Key(9)] public decimal DirectionThreshold { get; init; } = 0.20m;
    /// <summary>Gets the Moderate strength threshold.</summary>
    [Key(10)] public decimal ModerateThreshold { get; init; } = 0.40m;
    /// <summary>Gets the Strong strength threshold.</summary>
    [Key(11)] public decimal StrongThreshold { get; init; } = 0.65m;
    /// <summary>Gets the Extreme strength threshold.</summary>
    [Key(12)] public decimal ExtremeThreshold { get; init; } = 0.85m;
    /// <summary>Gets the ITI reversal threshold for Exhausting.</summary>
    [Key(13)] public decimal ExhaustingReversalThreshold { get; init; } = 0.25m;
    /// <summary>Gets the ITI reversal threshold for Reversing.</summary>
    [Key(14)] public decimal ReversingThreshold { get; init; } = 0.50m;
}
