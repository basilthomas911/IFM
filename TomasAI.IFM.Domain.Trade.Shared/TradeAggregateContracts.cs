using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared;

public enum TradeAssetFamily : byte { Unknown, Futures, FuturesOption, Equity, FixedIncome, Custom }
public enum TradeStrategyKind : byte { Unknown, FuturesOutright, VanillaOption, VerticalSpread, IronCondor, Custom }
public enum TradeOrderPositionType : byte { Unknown = 0, Opening = 1, Closing = 2 }
public enum TradeOrderStatus : byte { Draft, Approved, Ready, Executing, Completed, Cancelled, Expired }
public enum ExecutionChannel : byte { Manual, Broker }
public enum BrokerEnvironment : byte { Unknown = 0, Emulator = 1, Paper = 2, Live = 3 }
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
    // Key 1 is retained so previously serialized development payloads remain readable.
    // Broker-specific instrument identifiers are never populated or used by new code.
    [Key(1), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(2)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(3)] public int SignedQuantity { get; init; }
    [Key(4)] public decimal? LimitPrice { get; init; }
    [Key(5)] public string ContractKey { get; init; } = string.Empty;
    [Key(6)] public DateOnly? Expiry { get; init; }
    [Key(7)] public decimal? Strike { get; init; }
    [Key(8)] public byte? PutCall { get; init; }
    [Key(9)] public string ContractId { get; init; } = string.Empty;
    [Key(10)] public decimal CashMultiplier { get; init; }
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
    [Key(5)] public decimal? SignedNetDebitLimit { get; init; }
    [Key(6)] public decimal? MinimumSignedNetDebitLimit { get; init; }
    [Key(7)] public decimal? MaximumSignedNetDebitLimit { get; init; }
    [Key(8)] public decimal? TickIncrement { get; init; }
}

/// <summary>Broker-neutral approved order intent.</summary>
[MessagePackObject]
public sealed record TradeOrderDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 4;
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
    [Key(12)] public TradeOrderPositionType PositionType { get; init; }
    [Key(13)] public StrategyPositionId? TargetPositionId { get; init; }
    [Key(14)] public string BrokerAccountAlias { get; init; } = string.Empty;
    [Key(15)] public BrokerEnvironment BrokerEnvironment { get; init; }
    [Key(16)] public Guid PortfolioApprovalId { get; init; }
    [Key(17)] public string MicroExecutionProfileId { get; init; } = string.Empty;
    [Key(18)] public int MicroExecutionProfileVersion { get; init; }
    [Key(19)] public string MicroExecutionProfileHash { get; init; } = string.Empty;
    [Key(20)] public string AccountPromotionApprovalReference { get; init; } = string.Empty;
    [Key(21)] public decimal RequiredCapital { get; init; }
    [Key(22)] public decimal MaximumLoss { get; init; }
}

/// <summary>Normalized immutable fill evidence accepted by OrderExecution.</summary>
[MessagePackObject]
public sealed record ExecutionFillEvidence
{
    [Key(0)] public Guid ExecutionFillId { get; init; }
    [Key(1)] public Guid ExecutionAttemptId { get; init; }
    [Key(2)] public Guid ComponentId { get; init; }
    [Key(3)] public Guid TradeLegId { get; init; }
    [Key(4), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(5)] public int SignedQuantity { get; init; }
    [Key(6)] public decimal Price { get; init; }
    [Key(7)] public decimal Commission { get; init; }
    [Key(8)] public DateTime FilledAtUtc { get; init; }
    [Key(9)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(10)] public string ContractId { get; init; } = string.Empty;
}

/// <summary>Commission evidence retained until its external execution arrives.</summary>
[MessagePackObject]
public sealed record PendingExecutionCostEvidence
{
    [Key(0)] public string ExternalExecutionId { get; init; } = string.Empty;
    [Key(1)] public decimal Commission { get; init; }
}

/// <summary>State owned by one broker-neutral execution attempt.</summary>
[MessagePackObject]
public sealed record OrderExecutionDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 5;
    [Key(1)] public TradeOrderId TradeOrderId { get; init; }
    [Key(2)] public Guid ExecutionAttemptId { get; init; }
    [Key(3)] public ExecutionChannel Channel { get; init; }
    [Key(4)] public OrderExecutionStatus Status { get; init; }
    [Key(5)] public int OrderRevision { get; init; }
    [Key(6)] public TradeOrderComponentDefinition[] Components { get; init; } = [];
    [Key(7)] public ExecutionFillEvidence[] Fills { get; init; } = [];
    [Key(8)] public DateTime StartedAtUtc { get; init; }
    [Key(9)] public DateTime? CompletedAtUtc { get; init; }
    [Key(10)] public TradeOrderPositionType PositionType { get; init; }
    [Key(11)] public StrategyPositionId? TargetPositionId { get; init; }
    [Key(12)] public TradeOrderDefinition Order { get; init; } = new();
    [Key(13)] public PendingExecutionCostEvidence[] PendingFillCosts { get; init; } = [];
    [IgnoreMember] public OrderExecutionId Id => new(TradeOrderId, ExecutionAttemptId);
}

/// <summary>Accepted close-fill evidence for one existing strategy position.</summary>
[MessagePackObject]
public sealed record PositionCloseExecution
{
    [Key(0)] public StrategyPositionId PositionId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(2)] public Guid ExecutionAttemptId { get; init; }
    [Key(3)] public ExecutionFillEvidence[] Fills { get; init; } = [];
    [Key(4)] public DateTime CompletedAtUtc { get; init; }
}

/// <summary>Durable established trade created from accepted execution evidence.</summary>
[MessagePackObject]
public sealed record EstablishedTradeDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 3;
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
    [Key(13)] public ExecutionFillEvidence[] ClosingFills { get; init; } = [];
    [Key(14)] public DateTime? ClosedAtUtc { get; init; }
}

/// <summary>Current price and basis for one stable position leg.</summary>
[MessagePackObject]
public sealed record StrategyPositionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1), Obsolete("Use ContractId. Broker instrument IDs belong in the broker adapter.")]
    public uint LegacyMarketInstrumentId { get; init; }
    [Key(2)] public int SignedQuantity { get; init; }
    [Key(3)] public decimal OpeningPrice { get; init; }
    [Key(4)] public decimal CurrentPrice { get; init; }
    [Key(5)] public long LastSourceSequence { get; init; }
    [Key(6)] public DateTime LastPriceAtUtc { get; init; }
    [Key(7)] public string ContractId { get; init; } = string.Empty;
    [Key(8)] public TradeAssetFamily AssetFamily { get; init; }
    [Key(9)] public string ContractKey { get; init; } = string.Empty;
    [Key(10)] public DateOnly? Expiry { get; init; }
    [Key(11)] public decimal? Strike { get; init; }
    [Key(12)] public byte? PutCall { get; init; }
}

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
    [Key(6)] public StrategyPositionLeg[] Legs { get; init; } = [];
    [Key(7)] public decimal MarketValue { get; init; }
    [Key(8)] public decimal UnrealizedPnl { get; init; }
    [Key(9)] public decimal RealizedPnl { get; init; }
    [Key(10)] public DateTime AsOfUtc { get; init; }
    [Key(11)] public bool IsOpen { get; init; }
}

/// <summary>Compact destination stored in a realtime contract-to-position route bucket.</summary>
[MessagePackObject]
public readonly record struct PortfolioFundTradeLeg(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId,
    [property: Key(4)] Guid StrategyPositionId,
    [property: Key(5)] Guid TradeLegId,
    [property: Key(6)] TradeStrategyKind TradeType,
    [property: Key(7)] long Generation);

/// <summary>Compact normalized price change consumed by the realtime route index.</summary>
public readonly record struct PositionMarketTick(
    string ContractId,
    decimal Price,
    long SourceSequence,
    DateTime OccurredAtUtc);
