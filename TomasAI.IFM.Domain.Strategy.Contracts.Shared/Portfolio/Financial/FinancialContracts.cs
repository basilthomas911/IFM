using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingRequest
{
    [Key(0)] public int BookId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public LedgerTransactionKind TransactionKind { get; init; }
    [Key(3)] public DateOnly AccountingDate { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateOnly? SettlementDate { get; init; }
    [Key(6)] public string Currency { get; init; } = string.Empty;
    [Key(7)] public decimal Amount { get; init; }
    [Key(8)] public string Description { get; init; } = string.Empty;
    [Key(9)] public LedgerSourceReference Source { get; init; } = new();
    [Key(10)] public LedgerPostingRuleReference PostingRule { get; init; } = new();
    [Key(11)] public long? RelatedJournalId { get; init; }
    [Key(12)] public int? CounterpartyFundId { get; init; }
    [Key(13)] public LedgerEntryDraft[] Lines { get; init; } = [];
    [Key(14)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(15)] public LedgerMovementEvidence MovementEvidence { get; init; } = new();
    [Key(16)] public Guid? RelatedObligationId { get; init; }
    [Key(17)] public Guid? CapacityReservationId { get; init; }
    [Key(18)] public CapacityFundingComponent FundingComponent { get; init; }
}

/// <summary>A posted cash outflow can settle this named component of the exact consumed reservation.</summary>
public enum CapacityFundingComponent { Undefined=0, SettlementCash=1, EntryFees=2, MarginFunding=3 }

/// <summary>Versioned financial contract: LedgerPostingBatchRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchRequest
{
    [Key(0)] public int BookId { get; init; }
    [Key(1)] public LedgerPostingRequest[] Items { get; init; } = [];
    [Key(2)] public string BatchSourceReference { get; init; } = string.Empty;
    [Key(3)] public string ManifestHash { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: LedgerPostedTransaction; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostedTransaction
{
    [Key(0)] public int Ordinal { get; init; }
    [Key(1)] public long TransactionId { get; init; }
    [Key(2)] public long? JournalId { get; init; }
    [Key(3)] public string? JournalHash { get; init; }
    [Key(4)] public LedgerSourceReference Source { get; init; } = new();
    [Key(5)] public Guid? ObligationId { get; init; }
}

/// <summary>Versioned financial contract: LedgerPostingBatchReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int BookId { get; init; }
    [Key(4)] public LedgerPostedTransaction[] Items { get; init; } = [];
    [Key(5)] public string ManifestHash { get; init; } = string.Empty;
    [Key(6)] public string InputHash { get; init; } = string.Empty;
    [Key(7)] public long FinancialRevision { get; init; }
    [Key(8)] public DateTime CommittedAtUtc { get; init; }
    [Key(9)] public Guid CompletedEventId { get; init; }
}

/// <summary>Versioned financial contract: LedgerSourceReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerSourceReference
{
    [Key(0)] public string System { get; init; } = string.Empty;
    [Key(1)] public string SourceEntityId { get; init; } = string.Empty;
    [Key(2)] public Guid SourceEventId { get; init; }
    [Key(3)] public long SourceSequence { get; init; }
    [Key(4)] public string SourceContentHash { get; init; } = string.Empty;
    [Key(5)] public DateTime OccurredAtUtc { get; init; }
    [Key(6)] public long? LegacyTransactionId { get; init; }
    [Key(7)] public int? LegacyFundId { get; init; }
    [Key(8)] public int? OrderId { get; init; }
    [Key(9)] public int? TradeId { get; init; }
    [Key(10)] public string? FillId { get; init; }
}

/// <summary>Versioned financial contract: LedgerPostingRuleReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingRuleReference
{
    [Key(0)] public Guid RuleId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public string ContentHash { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: LedgerEntryDraft; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerEntryDraft
{
    [Key(0)] public int Ordinal { get; init; }
    [Key(1)] public int AccountId { get; init; }
    [Key(2)] public int? FundId { get; init; }
    [Key(3)] public PostingSide PostingSide { get; init; }
    [Key(4)] public decimal Amount { get; init; }
    [Key(5)] public string Currency { get; init; } = string.Empty;
    [Key(6)] public int? OrderId { get; init; }
    [Key(7)] public int? TradeId { get; init; }
    [Key(8)] public string SourceLineReference { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: LedgerMovementEvidence; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerMovementEvidence
{
    [Key(0)] public MovementStatus Status { get; init; }
    [Key(1)] public string SourceReference { get; init; } = string.Empty;
    [Key(2)] public DateTime ObservedAtUtc { get; init; }
    [Key(3)] public DateTime ReceivedAtUtc { get; init; }
    [Key(4)] public DateTime ValidUntilUtc { get; init; }
    [Key(5)] public string ContentHash { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: FinancialAuthorityReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record FinancialAuthorityReference
{
    [Key(0)] public long PortfolioVersion { get; init; }
    [Key(1)] public long FundMandateVersion { get; init; }
    [Key(2)] public int PolicyId { get; init; }
    [Key(3)] public long PolicyVersion { get; init; }
    [Key(4)] public Guid EnvelopeId { get; init; }
    [Key(5)] public long EnvelopeVersion { get; init; }
    [Key(6)] public long AssignmentVersion { get; init; }
    [Key(7)] public CatalogKey DeploymentKey { get; init; }
    [Key(8)] public long AuthorityEpoch { get; init; }
    [Key(9)] public string ValuationWatermark { get; init; } = string.Empty;
    [Key(10)] public string SourceWatermark { get; init; } = string.Empty;
    [Key(11)] public string FinancialSnapshotHash { get; init; } = string.Empty;
    [Key(12)] public DateTime ValidUntilUtc { get; init; }
}

/// <summary>Versioned financial contract: LedgerPostingReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int BookId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public long TransactionId { get; init; }
    [Key(6)] public long? JournalId { get; init; }
    [Key(7)] public string? JournalHash { get; init; }
    [Key(8)] public string InputHash { get; init; } = string.Empty;
    [Key(9)] public long FinancialRevision { get; init; }
    [Key(10)] public DateTime CommittedAtUtc { get; init; }
    [Key(11)] public Guid CompletedEventId { get; init; }
    [Key(12)] public LedgerSourceReference Source { get; init; } = new();
    [Key(13)] public Guid? ObligationId { get; init; }
}

/// <summary>Versioned financial contract: CapacityReservationRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityReservationRequest
{
    [Key(0)] public Guid ReservationId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int BookId { get; init; }
    [Key(3)] public int OrderId { get; init; }
    [Key(4)] public int[] TradeIds { get; init; } = [];
    [Key(5)] public Guid WorkflowId { get; init; }
    [Key(6)] public long InputWorkflowRevision { get; init; }
    [Key(7)] public Guid RiskInvocationId { get; init; }
    [Key(8)] public Guid RiskResultId { get; init; }
    [Key(9)] public string RiskAssessmentHash { get; init; } = string.Empty;
    [Key(10)] public Guid CompositionResultId { get; init; }
    [Key(11)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(12)] public string UnitCandidateHash { get; init; } = string.Empty;
    [Key(13)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(14)] public int StrategyUnits { get; init; }
    [Key(15)] public CapacityRequirements Requirements { get; init; } = new();
    [Key(16)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(17)] public FinancialEvidenceReference MarginEvidenceReference { get; init; } = new();
    [Key(18)] public string ExecutionEnvironment { get; init; } = string.Empty;
    [Key(19)] public DateTime ValidUntilUtc { get; init; }
    [Key(20)] public string AcceptedIntentReference { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: CapacityRequirements; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityRequirements
{
    [Key(0)] public string Currency { get; init; } = string.Empty;
    [Key(1)] public decimal SettlementCash { get; init; }
    [Key(2)] public decimal MarginFunding { get; init; }
    [Key(3)] public decimal FeeReserve { get; init; }
    [Key(4)] public decimal VariationReserve { get; init; }
    [Key(5)] public decimal LossCharge { get; init; }
    [Key(6)] public decimal MarginRequirement { get; init; }
    [Key(7)] public decimal GrossNotional { get; init; }
    [Key(8)] public int GrossContracts { get; init; }
    [Key(9)] public int PositionSlots { get; init; }
    [Key(10)] public CapacityExposure[] Exposures { get; init; } = [];
    [Key(11)] public int AccountingMethodVersion { get; init; }
    [Key(12)] public string ContentHash { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: CapacityExposure; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityExposure
{
    [Key(0)] public CapacityScopeKind ScopeKind { get; init; }
    [Key(1)] public string ScopeKey { get; init; } = string.Empty;
    [Key(2)] public CapacityMeasure Measure { get; init; }
    [Key(3)] public decimal Amount { get; init; }
    [Key(4)] public CapacityUnit Unit { get; init; }
    [Key(5)] public int MethodVersion { get; init; }
}

/// <summary>Versioned financial contract: FinancialEvidenceReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record FinancialEvidenceReference
{
    [Key(0)] public Guid EvidenceId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public string ContentHash { get; init; } = string.Empty;
    [Key(3)] public string Source { get; init; } = string.Empty;
    [Key(4)] public string Environment { get; init; } = string.Empty;
    [Key(5)] public DateTime ObservedAtUtc { get; init; }
    [Key(6)] public DateTime ValidUntilUtc { get; init; }
}

/// <summary>Versioned financial contract: CapacityReservationReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityReservationReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public Guid ReservationId { get; init; }
    [Key(3)] public int PortfolioId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public int BookId { get; init; }
    [Key(6)] public int OrderId { get; init; }
    [Key(7)] public int[] TradeIds { get; init; } = [];
    [Key(8)] public Guid RiskResultId { get; init; }
    [Key(9)] public string RiskAssessmentHash { get; init; } = string.Empty;
    [Key(10)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(11)] public string UnitCandidateHash { get; init; } = string.Empty;
    [Key(12)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(13)] public int StrategyUnits { get; init; }
    [Key(14)] public CapacityRequirements Requirements { get; init; } = new();
    [Key(15)] public long AuthorityEpoch { get; init; }
    [Key(16)] public long FinancialRevision { get; init; }
    [Key(17)] public DateTime GrantedAtUtc { get; init; }
    [Key(18)] public DateTime ValidUntilUtc { get; init; }
    [Key(19)] public string ExecutionEnvironment { get; init; } = string.Empty;
    [Key(20)] public Guid CompletedEventId { get; init; }
    [Key(21)] public string InputHash { get; init; } = string.Empty;
}

/// <summary>Versioned financial contract: CapacityLifecycleRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleRequest
{
    [Key(0)] public Guid ReservationId { get; init; }
    [Key(1)] public long ExpectedReservationVersion { get; init; }
    [Key(2)] public CapacityChangeKind ChangeKind { get; init; }
    [Key(3)] public Guid ExecutionId { get; init; }
    [Key(4)] public long ExecutionRevision { get; init; }
    [Key(5)] public LedgerSourceReference Source { get; init; } = new();
    [Key(6)] public int FilledUnits { get; init; }
    [Key(7)] public int CancelledUnits { get; init; }
    [Key(8)] public int RemainingUnits { get; init; }
    [Key(9)] public string RelatedPostingReference { get; init; } = string.Empty;
    [Key(10)] public string ExpectedRequirementsHash { get; init; } = string.Empty;
    [Key(11)] public int ClosedUnits { get; init; }
}

/// <summary>Versioned financial contract: CapacityLifecycleReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityLifecycleReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public Guid ReservationId { get; init; }
    [Key(3)] public long ReservationVersion { get; init; }
    [Key(4)] public long FinancialRevision { get; init; }
    [Key(5)] public ReservationStatus Status { get; init; }
    [Key(6)] public int FilledUnits { get; init; }
    [Key(7)] public int CancelledUnits { get; init; }
    [Key(8)] public int RemainingUnits { get; init; }
    [Key(9)] public string CurrentRequirementsHash { get; init; } = string.Empty;
    [Key(10)] public DateTime CommittedAtUtc { get; init; }
    [Key(11)] public Guid CompletedEventId { get; init; }
    [Key(12)] public string InputHash { get; init; } = string.Empty;
    [Key(13)] public int ClosedUnits { get; init; }
}

/// <summary>Business purpose determines accounting; signs never implicitly choose a transaction kind.</summary>
public enum LedgerTransactionKind { Undefined=0, DepositConfirmed=1, WithdrawalRequested=2, WithdrawalSettled=3, WithdrawalCancelled=4, FundTransfer=5, TradeSettlement=6, Commission=7, RealizedPnl=8, Valuation=9, Reversal=10, Adjustment=11, OpeningBalance=12 }
public enum PostingSide { Undefined=0, Debit=1, Credit=2 }
public enum MovementStatus { Undefined=0, Pending=1, Confirmed=2, Cancelled=3, Unknown=4 }
public enum ReservationStatus { Undefined=0, Reserved=1, Consumed=2, Working=3, PartiallyFilled=4, SubmissionUnknown=5, CancelPending=6, Filled=7, Released=8, Expired=9 }
public enum CapacityChangeKind { Undefined=0, Consume=1, RecordWorking=2, RecordFill=3, MarkSubmissionUnknown=4, RequestCancel=5, ConfirmCancel=6, ReleaseUnconsumed=7, ExpireUnconsumed=8, RecordPositionClose=9 }
public enum CapacityScopeKind { Undefined=0, Portfolio=1, Deployment=2, Fund=3, Underlying=4 }
public enum CapacityMeasure { Undefined=0, SettlementCash=1, LossCharge=2, Margin=3, GrossNotional=4, GrossContracts=5, PositionSlots=6, Delta=7, Gamma=8, Vega=9 }
public enum CapacityUnit { Undefined=0, Usd=1, Contracts=2, Positions=3, NormalizedDelta=4, NormalizedGamma=5, NormalizedVega=6 }
public enum FinancialCommitDisposition { NotCommitted=0, OutcomeUnknown=1, NoNewMutation=2 }
