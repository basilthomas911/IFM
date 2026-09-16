namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>Account mode; Unknown cannot authorize a broker dispatch.</summary>
public enum BrokerEnvironment : byte { Unknown = 0, Emulator = 1, Paper = 2, Live = 3 }
public enum BrokerOrderShape : byte { Unknown = 0, FuturesOutright = 1, VerticalSpread = 2, IronCondor = 3 }
public enum BrokerDispatchOutcome : byte { RejectedLocally = 0, AcceptedForDispatch = 1, OutcomeUnknown = 2 }
public enum BrokerObservationKind : byte { Unknown = 0, Acknowledged = 1, Execution = 2, Commission = 3, Rejected = 4, Cancelled = 5, AccountSnapshot = 6, GateChanged = 7, ConnectionChanged = 8, OrderCompleted = 9 }

/// <summary>Broker-neutral exact contract/ratio in an approved order.</summary>
public sealed record BrokerOrderLeg(Guid LegId, string ContractId, int SignedQuantity, decimal? Strike, DateOnly? Expiry, byte? PutCall, decimal CashMultiplier);

/// <summary>A single approved logical component and its signed net debit price envelope.</summary>
public sealed record BrokerOrderRequest(string AccountAlias, BrokerEnvironment Environment, string BrokerOrderId,
    Guid OperationId, Guid ComponentId, BrokerOrderShape Shape, bool IsClosing, BrokerOrderLeg[] Legs,
    decimal SignedNetDebitLimit, decimal MinimumLimit, decimal MaximumLimit, decimal TickIncrement,
    DateTime ValidUntilUtc, string ApprovalHash, string ContractReferenceHash, decimal RequiredCapital, decimal MaximumLoss);

public sealed record BrokerLimitUpdate(string AccountAlias, string BrokerOrderId, Guid OperationId, decimal NewSignedNetDebitLimit, int ExpectedRevision);
public sealed record BrokerCancelRequest(string AccountAlias, string BrokerOrderId, Guid OperationId, int ExpectedRevision);
public sealed record BrokerDispatchReceipt(BrokerDispatchOutcome Outcome, Guid OperationId, string BrokerOrderId, string Category, string Detail, DateTime RecordedAtUtc);

/// <summary>Normalized immutable authoritative callback fact.</summary>
public sealed record BrokerObservation
{
    public required BrokerObservationKind Kind { get; init; }
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

public sealed record BrokerAccountPosition(string ContractId, int SignedQuantity, decimal AveragePrice);
public sealed record BrokerAccountSnapshot(string AccountAlias, string Currency, decimal CashBalance, decimal AvailableFunds,
    bool Complete, bool NewRiskAllowed, long Generation, DateTime AsOfUtc, BrokerAccountPosition[] Positions);

/// <summary>One provider-neutral top-of-book quote supplied to simulated market execution.</summary>
public readonly record struct BrokerMarketQuote(string ContractId, decimal Bid, decimal Ask, int BidSize,
    int AskSize, DateTime MarketTimeUtc, long SourceEpoch, long SourceSequence);
