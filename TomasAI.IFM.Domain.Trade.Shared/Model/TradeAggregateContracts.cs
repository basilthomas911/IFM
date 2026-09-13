using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared.Model;

/// <summary>Identifies one broker-neutral order within its Portfolio and Fund authority.</summary>
[MessagePackObject]
public readonly record struct TradeOrderId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{PortfolioId}.{FundId}.{OrderId}");

    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0;
}

/// <summary>Identifies one established trade created from a component of a Trade Order.</summary>
[MessagePackObject]
public readonly record struct TradeEntityId(
    [property: Key(0)] TradeOrderId TradeOrder,
    [property: Key(1)] int TradeId) : IActorEntityId
{
    public TradeEntityId(int portfolioId, int fundId, int orderId, int tradeId)
        : this(new TradeOrderId(portfolioId, fundId, orderId), tradeId) { }

    [IgnoreMember] public int PortfolioId => TradeOrder.PortfolioId;
    [IgnoreMember] public int FundId => TradeOrder.FundId;
    [IgnoreMember] public int OrderId => TradeOrder.OrderId;
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{TradeOrder.Format()}.{TradeId}");
    [IgnoreMember] public bool IsValid => TradeOrder.IsValid && TradeId > 0;
}

/// <summary>Identifies one execution attempt for one exact Trade Order stream.</summary>
[MessagePackObject]
public readonly record struct OrderExecutionId(
    [property: Key(0)] TradeOrderId TradeOrder,
    [property: Key(1)] Guid ExecutionAttemptId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{TradeOrder.Format()}.{ExecutionAttemptId:N}");
    [IgnoreMember] public bool IsValid => TradeOrder.IsValid && ExecutionAttemptId != Guid.Empty;
}

/// <summary>Identifies a concrete strategy-position stream for an established trade.</summary>
[MessagePackObject]
public readonly record struct StrategyPositionId(
    [property: Key(0)] TradeEntityId Trade,
    [property: Key(1)] Guid PositionId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Trade.Format()}.{PositionId:N}");
    [IgnoreMember] public bool IsValid => Trade.IsValid && PositionId != Guid.Empty;
}

public enum TradeAssetFamily : byte { Unknown, Futures, FuturesOption, Equity, FixedIncome, Custom }
public enum TradeStrategyKind : byte { Unknown, FuturesOutright, SingleOption, VerticalSpread, IronCondor, Custom }
public enum TradeOrderStatus : byte { Draft, Approved, Ready, Executing, Completed, Cancelled, Expired }
public enum ExecutionChannel : byte { Manual, Broker }
public enum OrderExecutionStatus : byte { Pending, Submitted, PartiallyFilled, Filled, Cancelled, Rejected, Reconciled }
public enum EstablishedTradeStatus : byte { Open, Closing, Closed, Corrected }
public enum StrategyPositionPhase : byte { Open, MarkToMarket, EndOfDay, Close, Correction }
public enum MarketRouteLookupOutcome : byte
{
    Routed,
    NoOpenPosition,
    StaleRoute,
    UnknownInstrument,
    InvalidTick,
    DuplicateOrOutOfOrder
}

/// <summary>One stable proposed or established trade leg.</summary>
[MessagePackObject]
public sealed record TradeLegDefinition
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public uint MarketInstrumentId { get; init; }
    [Key(2)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(3)] public int SignedQuantity { get; init; }
    [Key(4)] public decimal? LimitPrice { get; init; }
    [Key(5)] public string ContractKey { get; init; } = string.Empty;
    [Key(6)] public DateOnly? Expiry { get; init; }
    [Key(7)] public decimal? Strike { get; init; }
    [Key(8)] public byte? PutCall { get; init; }
}

/// <summary>A separately executable component in a generic order.</summary>
[MessagePackObject]
public sealed record TradeOrderComponentDefinition
{
    [Key(0)] public Guid ComponentId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(2)] public TradeLegDefinition[] Legs { get; init; } = [];
    [Key(3)] public bool PermitBalancedPartialAcceptance { get; init; }
    [Key(4)] public int ReservedTradeId { get; init; }
}

/// <summary>Broker-neutral approved order intent.</summary>
[MessagePackObject]
public sealed record TradeOrderDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public TradeOrderId Id { get; init; }
    [Key(2)] public int Revision { get; init; }
    [Key(3)] public TradeOrderStatus Status { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [Key(7)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [Key(8)] public string DefinitionHash { get; init; } = string.Empty;
    [Key(9)] public Guid? BoundExecutionAttemptId { get; init; }
    [Key(10)] public ExecutionChannel? BoundExecutionChannel { get; init; }
    [Key(11)] public DateTime? ExecutionBoundAtUtc { get; init; }
}

/// <summary>Normalized immutable fill evidence accepted by OrderExecution.</summary>
[MessagePackObject]
public sealed record ExecutionFillEvidence
{
    [Key(0)] public Guid ExecutionFillId { get; init; }
    [Key(1)] public Guid ExecutionAttemptId { get; init; }
    [Key(2)] public Guid ComponentId { get; init; }
    [Key(3)] public Guid TradeLegId { get; init; }
    [Key(4)] public uint MarketInstrumentId { get; init; }
    [Key(5)] public int SignedQuantity { get; init; }
    [Key(6)] public decimal Price { get; init; }
    [Key(7)] public decimal Commission { get; init; }
    [Key(8)] public DateTime FilledAtUtc { get; init; }
    [Key(9)] public string ExternalExecutionId { get; init; } = string.Empty;
}

/// <summary>State owned by one broker-neutral execution attempt.</summary>
[MessagePackObject]
public sealed record OrderExecutionDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public TradeOrderId TradeOrderId { get; init; }
    [Key(2)] public Guid ExecutionAttemptId { get; init; }
    [Key(3)] public ExecutionChannel Channel { get; init; }
    [Key(4)] public OrderExecutionStatus Status { get; init; }
    [Key(5)] public int OrderRevision { get; init; }
    [Key(6)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [Key(7)] public ExecutionFillEvidence[] Fills { get; init; } = [];
    [Key(8)] public DateTime StartedAtUtc { get; init; }
    [Key(9)] public DateTime? CompletedAtUtc { get; init; }
    [IgnoreMember] public OrderExecutionId Id => new(TradeOrderId, ExecutionAttemptId);
}

/// <summary>Durable established trade created from accepted execution evidence.</summary>
[MessagePackObject]
public sealed record EstablishedTradeDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public TradeEntityId Id { get; init; }
    [Key(2)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(3)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(4)] public Guid SourceComponentId { get; init; }
    [Key(5)] public Guid ExecutionAttemptId { get; init; }
    [Key(6)] public EstablishedTradeStatus Status { get; init; }
    [Key(7)] public TradeLegDefinition[] Legs { get; init; } = [];
    [Key(8)] public ExecutionFillEvidence[] OriginalFills { get; init; } = [];
    [Key(9)] public decimal OpeningValue { get; init; }
    [Key(10)] public decimal OpeningCommission { get; init; }
    [Key(11)] public DateTime EstablishedAtUtc { get; init; }
    [Key(12)] public int EvidenceRevision { get; init; }
}

/// <summary>Current price and basis for one stable position leg.</summary>
[MessagePackObject]
public sealed record StrategyPositionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public uint MarketInstrumentId { get; init; }
    [Key(2)] public int SignedQuantity { get; init; }
    [Key(3)] public decimal OpeningPrice { get; init; }
    [Key(4)] public decimal CurrentPrice { get; init; }
    [Key(5)] public long LastSourceSequence { get; init; }
    [Key(6)] public DateTime LastPriceAtUtc { get; init; }
}

/// <summary>One coherent whole-strategy position version.</summary>
[MessagePackObject]
public sealed record StrategyPositionSnapshot
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public StrategyPositionId Id { get; init; }
    [Key(2)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(3)] public StrategyPositionPhase Phase { get; init; }
    [Key(4)] public long PositionSequence { get; init; }
    [Key(5)] public long RouteGeneration { get; init; }
    [Key(6)] public StrategyPositionLeg[] Legs { get; init; } = [];
    [Key(7)] public decimal MarketValue { get; init; }
    [Key(8)] public decimal UnrealizedPnl { get; init; }
    [Key(9)] public decimal RealizedPnl { get; init; }
    [Key(10)] public DateTime AsOfUtc { get; init; }
    [Key(11)] public bool IsOpen { get; init; }
}

/// <summary>Compact destination stored in a realtime contract-to-position route bucket.</summary>
[MessagePackObject]
public readonly record struct MarketPositionRoute(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId,
    [property: Key(4)] Guid StrategyPositionId,
    [property: Key(5)] Guid TradeLegId,
    [property: Key(6)] TradeStrategyKind StrategyKind,
    [property: Key(7)] string PositionActor,
    [property: Key(8)] string PositionActorThreadId,
    [property: Key(9)] long Generation);

/// <summary>Compact normalized price change consumed by the realtime route index.</summary>
public readonly record struct PositionMarketTick(
    uint MarketInstrumentId,
    decimal Price,
    long SourceSequence,
    DateTime OccurredAtUtc);
