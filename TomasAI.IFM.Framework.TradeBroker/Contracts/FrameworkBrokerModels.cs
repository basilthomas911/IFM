namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>The exact first-release strategy shape supported by a provider.</summary>
public enum FrameworkOrderShape : byte
{
    Unknown = 0,
    FuturesOutright = 1,
    VerticalSpread = 2,
    IronCondor = 3
}

public enum FrameworkOrderType : byte { Unknown = 0, Market = 1, Limit = 2 }
public enum FrameworkOrderAlgorithm : byte { None = 0, Adaptive = 1 }

/// <summary>A local dispatch receipt, never an acknowledgement or execution.</summary>
public enum FrameworkDispatchOutcome : byte
{
    RejectedLocally = 0,
    AcceptedForDispatch = 1,
    OutcomeUnknown = 2
}

/// <summary>The normalized critical observation type produced by a broker port.</summary>
public enum FrameworkObservationKind : byte
{
    Unknown = 0,
    Acknowledged = 1,
    Execution = 2,
    Commission = 3,
    Rejected = 4,
    Cancelled = 5,
    AccountSnapshot = 6,
    GateChanged = 7,
    ConnectionChanged = 8,
    OrderCompleted = 9
}

/// <summary>One exact contract and signed quantity in a broker-neutral request.</summary>
public sealed record FrameworkOrderLeg(
    Guid LegId,
    string ContractId,
    int SignedQuantity,
    decimal? Strike,
    DateOnly? Expiry,
    byte? PutCall,
    decimal CashMultiplier);

/// <summary>One approved, separately executable order component.</summary>
public sealed record FrameworkOrderRequest(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    Guid ComponentId,
    FrameworkOrderShape Shape,
    bool IsClosing,
    FrameworkOrderLeg[] Legs,
    decimal SignedNetDebitLimit,
    decimal MinimumLimit,
    decimal MaximumLimit,
    decimal TickIncrement,
    DateTime ValidUntilUtc,
    string ApprovalHash,
    string ContractReferenceHash,
    decimal RequiredCapital,
    decimal MaximumLoss,
    FrameworkOrderType OrderType = FrameworkOrderType.Limit,
    FrameworkOrderAlgorithm Algorithm = FrameworkOrderAlgorithm.None);

/// <summary>The bounded price-only change authorized by the current order envelope.</summary>
public sealed record FrameworkLimitUpdate(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    decimal NewSignedNetDebitLimit,
    int ExpectedRevision);

/// <summary>An individual cancel request for a known logical order.</summary>
public sealed record FrameworkCancelRequest(
    string AccountAlias,
    string BrokerOrderId,
    Guid OperationId,
    int ExpectedRevision);

/// <summary>The local result of passing an operation to the provider boundary.</summary>
public sealed record FrameworkDispatchReceipt(
    FrameworkDispatchOutcome Outcome,
    Guid OperationId,
    string BrokerOrderId,
    string Category,
    string Detail,
    DateTime RecordedAtUtc);

/// <summary>One authoritative normalized broker fact, with exact correlation.</summary>
public sealed record FrameworkBrokerObservation
{
    public required FrameworkObservationKind Kind { get; init; }
    public required Guid ObservationId { get; init; }
    public required string AccountAlias { get; init; }
    public required string BrokerOrderId { get; init; }
    public Guid OperationId { get; init; }
    public Guid ComponentId { get; init; }
    public Guid LegId { get; init; }
    public string ContractId { get; init; } = string.Empty;
    public string ExternalExecutionId { get; init; } = string.Empty;
    public int SignedQuantity { get; init; }
    public decimal Price { get; init; }
    public decimal Commission { get; init; }
    public int OrderRevision { get; init; }
    public long SourceEpoch { get; init; }
    public long SourceSequence { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

/// <summary>A coherent synthetic or external account observation.</summary>
public sealed record FrameworkAccountSnapshot
{
    public required string AccountAlias { get; init; }
    public required string Currency { get; init; }
    public decimal CashBalance { get; init; }
    public decimal AvailableFunds { get; init; }
    public bool Complete { get; init; }
    public bool NewRiskAllowed { get; init; }
    public long Generation { get; init; }
    public DateTime AsOfUtc { get; init; }
    public FrameworkAccountPosition[] Positions { get; init; } = [];
}

/// <summary>One contract-level position in an account snapshot.</summary>
public sealed record FrameworkAccountPosition(string ContractId, int SignedQuantity, decimal AveragePrice);

/// <summary>One top-of-book quote used by simulated market execution.</summary>
public readonly record struct FrameworkMarketQuote(string ContractId, decimal Bid, decimal Ask, int BidSize,
    int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);
