using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>One coherent whole-strategy position version.</summary>
[MessagePackObject]
public sealed record StrategyPositionSnapshot
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 2;
    [Key(1)] public StrategyPositionId Id { get; init; }
    [Key(2)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(3)] public StrategyPositionPhase Phase { get; init; }
    [Key(4)] public long PositionSequence { get; init; }
    [Key(5)] public long RouteGeneration { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public StrategyPositionLeg[] Legs { get; init; } = [];
    [Key(7)] public decimal MarketValue { get; init; }
    [Key(8)] public decimal UnrealizedPnl { get; init; }
    [Key(9)] public decimal RealizedPnl { get; init; }
    [Key(10)] public DateTime AsOfUtc { get; init; }
    [Key(11)] public bool IsOpen { get; init; }
    private ExecutionFillEvidence[] closingFills = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(12)] public ExecutionFillEvidence[] ClosingFills { get => closingFills; init => closingFills = value ?? []; }
    /// <summary>Gets the exchange-session value date, independent of UTC calendar boundaries.</summary>
    [Key(13)] public DateOnly ValueDate { get; init; }
    /// <summary>Gets the latest date whose daily position has been sealed against market updates.</summary>
    [Key(14)] public DateOnly LatestSealedValueDate { get; init; }
    /// <summary>Gets the last actual market observation; finalization does not fabricate a quote time.</summary>
    [Key(15)] public DateTime LastMarketObservationUtc { get; init; }
    /// <summary>Gets the confirmed session close boundary for an EOD row.</summary>
    [Key(16)] public DateTime? CloseBoundaryUtc { get; init; }
    /// <summary>Gets cumulative realized plus unrealized PnL at the start of this value date.</summary>
    [Key(17)] public decimal DailyOpeningPnl { get; init; }
    /// <summary>Quantity-weighted daily price-point change. Key 18 preserves legacy source events; this is not currency PnL.</summary>
    [Key(18)] public decimal DailyPnlPoints { get; init; }
    /// <summary>Gets the stable timestamp of this history row; daily MTM and EOD share the same row.</summary>
    [Key(19)] public DateTime HistoryAsOfUtc { get; init; }
    /// <summary>Gets the stable history-row sequence; current-position sequence advances independently.</summary>
    [Key(20)] public long HistoryPositionSequence { get; init; }
    /// <summary>Backend-valued history display; absent on old stored events until query enrichment.</summary>
    [Key(21)] public StrategyPositionHistoryValuation? HistoryValuation { get; init; }
    /// <summary>Daily PnL in account currency, net of actual daily commissions. Null means currency evidence is unavailable.</summary>
    [Key(22)] public decimal? DailyPnl { get; init; }
    [Key(23)] public decimal? ContractCashMultiplier { get; init; }
    [Key(24)] public decimal? OpeningCommission { get; init; }
    [Key(25)] public DateOnly? OpeningValueDate { get; init; }

}

[MessagePackObject]
public sealed record StrategyPositionHistoryValuation(
    [property: Key(0)] DateOnly ValueDate, [property: Key(1)] int DaysToExpiry,
    [property: Key(2)] int Quantity, [property: Key(3)] decimal NetPrice,
    [property: Key(4)] decimal Commission);
