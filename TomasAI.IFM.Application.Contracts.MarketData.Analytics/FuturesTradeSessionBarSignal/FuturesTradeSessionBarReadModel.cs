using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>
/// Represents one immutable, session-aligned OHLCV observation shared by all bar-derived analytics actors.
/// </summary>
[MessagePackObject]
public sealed record FuturesTradeSessionBarReadModel
{
    /// <summary>Gets the exact provider-neutral market series.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }

    /// <summary>Gets the deterministic identity of this immutable observation.</summary>
    [Key(1)] public FuturesTradeSessionBarId ObservationId { get; init; }

    /// <summary>Gets the actual futures contract that supplied the closing data.</summary>
    [Key(2)] public string ContractId { get; init; } = string.Empty;

    /// <summary>Gets the futures trading value date.</summary>
    [Key(3)] public DateOnly ValueDate { get; init; }

    /// <summary>Gets the observation timeframe.</summary>
    [Key(4)] public TimeFrameType TimeFrame { get; init; }

    /// <summary>Gets the inclusive UTC interval start.</summary>
    [Key(5)] public DateTimeOffset IntervalStartUtc { get; init; }

    /// <summary>Gets the exclusive UTC interval end.</summary>
    [Key(6)] public DateTimeOffset IntervalEndUtc { get; init; }

    /// <summary>Gets the first accepted trade price.</summary>
    [Key(7)] public decimal Open { get; init; }

    /// <summary>Gets the highest accepted trade price.</summary>
    [Key(8)] public decimal High { get; init; }

    /// <summary>Gets the lowest accepted trade price.</summary>
    [Key(9)] public decimal Low { get; init; }

    /// <summary>Gets the last accepted trade price.</summary>
    [Key(10)] public decimal Close { get; init; }

    /// <summary>Gets the sum of accepted trade sizes.</summary>
    [Key(11)] public decimal Volume { get; init; }

    /// <summary>Gets the number of accepted trades.</summary>
    [Key(12)] public long TradeCount { get; init; }

    /// <summary>Gets the exact sum of price multiplied by trade size.</summary>
    [Key(13)] public decimal PriceVolumeSum { get; init; }

    /// <summary>Gets the first accepted source sequence.</summary>
    [Key(14)] public long FirstSourceSequence { get; init; }

    /// <summary>Gets the last accepted source sequence.</summary>
    [Key(15)] public long LastSourceSequence { get; init; }

    /// <summary>Gets the first exchange event time included in the observation.</summary>
    [Key(16)] public DateTimeOffset FirstMarketEventUtc { get; init; }

    /// <summary>Gets the last exchange event time included in the observation.</summary>
    [Key(17)] public DateTimeOffset LastMarketEventUtc { get; init; }

    /// <summary>Gets the UTC time at which the observation was finalized.</summary>
    [Key(18)] public DateTimeOffset CalculatedAtUtc { get; init; }

    /// <summary>Gets the serialized observation schema version.</summary>
    [Key(19)] public ushort SchemaVersion { get; init; } = 1;

    /// <summary>Gets the observation-calculation implementation version.</summary>
    [Key(20)] public string CalculationVersion { get; init; } = string.Empty;

    /// <summary>Gets whether the complete interval was observed.</summary>
    [Key(21)] public bool IsComplete { get; init; }

    /// <summary>Gets whether the observation passed input and formula validation.</summary>
    [Key(22)] public bool IsValid { get; init; }

    /// <summary>Gets stable reasons explaining an invalid observation.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(23)] public MarketSignalValidationIssue[] ValidationIssues { get; init; } = [];

    /// <summary>Gets how this observation was calculated.</summary>
    [Key(24)] public MarketSignalCalculationMethod CalculationMethod { get; init; }

    /// <summary>Gets the Databento live-stream epoch that supplied this observation, when applicable.</summary>
    [Key(25)] public Guid StreamEpochId { get; init; }
}
