using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public enum LedgerConfigurationAction
{
    Undefined=0, CreateBook=1, AddAccountVersion=2, AddPostingRuleVersion=3,
    RetireAccount=4, RetirePostingRule=5, OpenPeriod=6, ClosePeriod=7, ReopenPeriod=8,
    RefreshAuthority=9, Reconcile=10, QualifyDevelopmentBook=11
}

/// <summary>Versioned configuration intent. Generated identifiers are supplied by the application service.</summary>
[MessagePackObject]
public sealed record LedgerConfigurationRequest
{
    [Key(0)] public LedgerConfigurationAction Action { get; init; }
    [Key(1)] public int BookId { get; init; }
    [Key(2)] public FinancialBookConfiguration? Book { get; init; }
    [Key(3)] public LedgerAccountDefinition[] Accounts { get; init; }=[];
    [Key(4)] public LedgerPostingRule[] Rules { get; init; }=[];
    [Key(5)] public Guid PeriodId { get; init; }
    [Key(6)] public DateOnly PeriodStart { get; init; }
    [Key(7)] public DateOnly PeriodEnd { get; init; }
    [Key(8)] public long ExpectedVersion { get; init; }
    [Key(9)] public string SourceCut { get; init; }=string.Empty;
    [Key(10)] public string Reason { get; init; }=string.Empty;
    [Key(11)] public Guid? ReconciliationId { get; init; }
}

[MessagePackObject]
public sealed record LedgerConfigurationReceipt(
    [property:Key(0)] Guid OperationId, [property:Key(1)] int BookId,
    [property:Key(2)] LedgerConfigurationAction Action, [property:Key(3)] long FinancialRevision,
    [property:Key(4)] DateTime CommittedAtUtc, [property:Key(5)] Guid? ReconciliationId,
    [property:Key(6)] string OperatingState);

[MessagePackObject]
public sealed record LedgerBalanceDifference([property:Key(0)] int AccountId,[property:Key(1)] int? FundId,
    [property:Key(2)] decimal JournalDebits,[property:Key(3)] decimal JournalCredits,
    [property:Key(4)] decimal RecordedDebits,[property:Key(5)] decimal RecordedCredits);

[MessagePackObject]
public sealed record LedgerReconciliationResult([property:Key(0)] Guid ReconciliationId,[property:Key(1)] long FinancialRevision,
    [property:Key(2)] long JournalCount,[property:Key(3)] long EntryCount,[property:Key(4)] decimal Debits,
    [property:Key(5)] decimal Credits,[property:Key(6)] LedgerBalanceDifference[] Differences,
    [property:Key(7)] string SourceCut,[property:Key(8)] string ContentHash);
