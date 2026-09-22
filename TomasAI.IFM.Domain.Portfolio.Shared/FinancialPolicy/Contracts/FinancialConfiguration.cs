using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact account version selected by an immutable posting rule.</summary>
[MessagePackObject]
public sealed record LedgerAccountBinding([property: Key(0)] int AccountId, [property: Key(1)] long Version);

/// <summary>Configured account bindings; ordinary transaction callers cannot choose alternative cash accounts.</summary>
[MessagePackObject]
public sealed record LedgerPostingRule([property: Key(0)] Guid RuleId, [property: Key(1)] int Version,
    [property: Key(2)] string ContentHash, [property: Key(3)] LedgerTransactionKind Kind,
    [property: Key(4)] LedgerAccountBinding Debit, [property: Key(5)] LedgerAccountBinding Credit,
    [property: Key(6)] bool RequiresConfirmedMovement, [property: Key(7)] LedgerAccountBinding? ValuationAsset = null,
    [property: Key(8)] LedgerAccountBinding? UnrealizedPnl = null);

public sealed record PlannedLedgerLine(int Ordinal, LedgerAccountBinding Account, int? FundId,
    decimal Debit, decimal Credit, int? OrderId, int? TradeId, string SourceLineReference);
public sealed record LedgerPostingPlan(IReadOnlyList<PlannedLedgerLine> Lines, decimal WithdrawalObligationDelta,
    bool IsDiscretionarySpending, bool IsActualFinancialFact, string ContentHash = "");

[MessagePackObject]
public sealed record CapacityLimit([property: Key(0)] CapacityScopeKind ScopeKind, [property: Key(1)] string ScopeKey,
    [property: Key(2)] CapacityMeasure Measure, [property: Key(3)] CapacityUnit Unit,
    [property: Key(4)] decimal Maximum, [property: Key(5)] bool Enabled = true);
[MessagePackObject]
public sealed record CapacityUsed([property: Key(0)] CapacityScopeKind ScopeKind, [property: Key(1)] string ScopeKey,
    [property: Key(2)] CapacityMeasure Measure, [property: Key(3)] CapacityUnit Unit,
    [property: Key(4)] decimal Held, [property: Key(5)] decimal Working, [property: Key(6)] decimal Position);

/// <summary>Portfolio-owned spending authority; its source versions must still match committed configuration.</summary>
[MessagePackObject]
public sealed record FinancialFundAuthority
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public bool CanSpend { get; init; }
    [Key(2)] public FinancialAuthorityReference Reference { get; init; } = new();
    [Key(3)] public CapacityLimit[] Limits { get; init; } = [];
    [Key(4)] public long PortfolioStreamVersion { get; init; }
    [Key(5)] public long FundStreamVersion { get; init; }
    [Key(6)] public long PolicyStreamVersion { get; init; }
    [Key(7)] public FinancialDeploymentAuthority[] Deployments { get; init; }=[];
}

/// <summary>One exact permitted deployment; shared Portfolio/Fund limits remain on the Fund authority.</summary>
[MessagePackObject]
public sealed record FinancialDeploymentAuthority([property:Key(0)] FinancialAuthorityReference Reference,
    [property:Key(1)] CapacityLimit[] Limits,[property:Key(2)] decimal MaximumRiskPerTrade=0);

[MessagePackObject]
public sealed record FinancialBookConfiguration
{
    [Key(0)] public int BookId { get; init; }
    [Key(1)] public int PortfolioId { get; init; }
    [Key(2)] public Guid AccountingEntityId { get; init; }
    [Key(3)] public string Currency { get; init; } = "USD";
    [Key(4)] public string ExecutionAccountReference { get; init; } = string.Empty;
    [Key(5)] public string Environment { get; init; } = string.Empty;
    [Key(6)] public FinancialFundAuthority[] Funds { get; init; } = [];
    [Key(7)] public long AuthorityEpoch { get; init; }
    [Key(8)] public string SourceWatermark { get; init; } = string.Empty;
    [Key(9)] public string ValuationWatermark { get; init; } = string.Empty;
    [Key(10)] public bool MigrationQualified { get; init; }
}

[MessagePackObject]
public sealed record LedgerAccountDefinition([property: Key(0)] int AccountId, [property: Key(1)] long Version,
    [property: Key(2)] string Category, [property: Key(3)] PostingSide NormalSide,
    [property: Key(4)] bool FundDimensionRequired, [property: Key(5)] string ContentHash);

public sealed record PreparedLedgerPosting(long TransactionId, long? JournalId, LedgerPostingRequest Request);
public sealed record LedgerFinancialSnapshot(FinancialBookConfiguration Book, long Revision,
    IReadOnlyList<LedgerPostingRule> Rules, IReadOnlyDictionary<int, decimal> SettledCash,
    IReadOnlyDictionary<int, decimal> PendingWithdrawals, IReadOnlyDictionary<int, decimal> ReservedFunding);

/// <summary>Authoritative lifecycle snapshot, including transitions committed by either capacity Function.</summary>
[MessagePackObject]
public sealed record ReservationSnapshot([property:Key(0)] Guid ReservationId, [property:Key(1)] long Version, [property:Key(2)] ReservationStatus Status,
    [property:Key(3)] int StrategyUnits, [property:Key(4)] int FilledUnits, [property:Key(5)] int CancelledUnits, [property:Key(6)] int RemainingUnits,
    [property:Key(7)] string RequirementsHash, [property:Key(8)] DateTime ValidUntilUtc, [property:Key(9)] Guid? ExecutionId, [property:Key(10)] long ExecutionRevision, [property:Key(11)] int ClosedUnits = 0);
public sealed record CapacityTransition(ReservationStatus Status, int FilledUnits, int CancelledUnits,
    int RemainingUnits, decimal HeldFraction, decimal WorkingFraction, decimal PositionFraction, int ClosedUnits = 0);
