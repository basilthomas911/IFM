using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

/// <summary>One atomically published display revision. Numeric risk values use model units, not cash exposure.</summary>
[MessagePackObject]
public sealed record TradeRealtimeSnapshot
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public TradeEntityId TradeId { get; init; }
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public Guid MonitoringGeneration { get; init; }
    [Key(4)] public long SnapshotRevision { get; init; }
    [Key(5)] public DateTimeOffset PublishedAtUtc { get; init; }
    [Key(6)] public ImmutableArray<TradeLegRealtimeSnapshot> Legs { get; init; } = [];
    [Key(7)] public ImmutableArray<TradeSpreadRealtimeSnapshot> Spreads { get; init; } = [];
    [Key(8)] public StrategyPositionSnapshot? Position { get; init; }
    [Key(9)] public StrategyTradePlanSnapshot? Plan { get; init; }
    [Key(10)] public string Status { get; init; } = "Initializing";
    [Key(11)] public string PlanStatus { get; init; } = "Unavailable";
    [Key(12)] public StrategyPositionId PositionId { get; init; }
    [Key(13)] public TradeRealtimeSummary Summary { get; init; } = new();
    [Key(14)] public int CompositionVersion { get; init; }
    /// <summary>Client-only expiry changes do not consume or invent backend snapshot revisions.</summary>
    [Key(15)] public long QualityRevision { get; init; }
    /// <summary>Last complete plan while the current plan is incomplete; retained display values never change PlanStatus.</summary>
    [Key(16)] public StrategyTradePlanSnapshot? LastAvailablePlan { get; init; }
}

/// <summary>Signed contract-weighted model sensitivities: Theta/year, Vega and Rho/unit decimal. PnL is authoritative position points.</summary>
[MessagePackObject]
public sealed record TradeRealtimeSummary
{
    [Key(0)] public double? UnderlyingPrice { get; init; }
    [Key(1)] public double? NetDelta { get; init; }
    [Key(2)] public double? NetGamma { get; init; }
    [Key(3)] public double? NetVega { get; init; }
    [Key(4)] public double? NetTheta { get; init; }
    [Key(5)] public double? NetRho { get; init; }
    [Key(6)] public decimal? PositionValue { get; init; }
    [Key(7)] public decimal? DailyPnlPoints { get; init; }
    [Key(8)] public decimal? NetSpread { get; init; }
}

[MessagePackObject]
public sealed record TradeLegRealtimeSnapshot
{
    [Key(0)] public TradeLegDefinition Definition { get; init; } = new();
    [Key(1)] public FuturesOptionTickDataV2ReadModel? Observation { get; init; }
    [Key(2)] public StrategyPositionLeg? Position { get; init; }
    [Key(3)] public string QuoteStatus { get; init; } = "Initializing";
    [Key(4)] public string PricingStatus { get; init; } = "Initializing";
    [Key(5)] public double? LastTradePrice { get; init; }
    [Key(6)] public long LastTradeSequence { get; init; }
    [Key(7)] public DateTimeOffset LastTradeAtUtc { get; init; }
    /// <summary>Qualified strategy scenario; its Vega is per volatility percentage point and Theta per day.</summary>
    [Key(8)] public IronCondorTradePlanLegObservation? Scenario { get; init; }
    /// <summary>Last valid calculation, with original timestamps, retained while a later calculation is pending or unavailable.</summary>
    [Key(9)] public FuturesOptionTickDataV2ReadModel? LastValidRisk { get; init; }
}

[MessagePackObject]
public sealed record TradeSpreadRealtimeSnapshot
{
    [Key(0)] public byte PutCall { get; init; }
    [Key(1)] public decimal? NetSpread { get; init; }
    [Key(2)] public decimal? TradeValue { get; init; }
    [Key(3)] public decimal? TradePnl { get; init; }
    [Key(4)] public double? OtmProbability { get; init; }
    [Key(5)] public decimal? ForwardPrice { get; init; }
}
