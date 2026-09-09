using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject(AllowPrivate=true)]
public sealed record FinancialQuery<TRequest,TResult> : IQuery<FinancialRead<TResult>> where TResult:class
{
    [Key(0)] public int SchemaVersion { get; init; }=1;
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public LedgerPortfolioId QueryEntityId { get; init; }=new(0);
    [Key(3)] public FinancialReadScope Scope { get; init; }=new();
    [Key(4)] public TRequest Parameters { get; init; }=default!;
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public DateTime RequestedAtUtc { get; init; }
    [IgnoreMember] public int ErrorCode=>FinancialReasons.PersistenceFailed;
    [IgnoreMember] public string? QueryParams=>QueryEntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId=>QueryEntityId;
}

[MessagePackObject]
public sealed record FinancialReadScope
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int? FundId { get; init; }
    [Key(2)] public FinancialAccess Access { get; init; }=new(string.Empty,[]);
}
public enum FinancialReadStatus { Unknown=0,Found=1,NotFound=2,Unavailable=3 }
[MessagePackObject]
public sealed record FinancialRead<T>([property:Key(0)] FinancialReadStatus Status,[property:Key(1)] T? Value,
    [property:Key(2)] long FinancialRevision,[property:Key(3)] DateTime ObservedAtUtc) where T:class;
[MessagePackObject] public sealed record GetPostingReceiptRequest([property:Key(0)] Guid OperationId);
[MessagePackObject] public sealed record GetJournalRequest([property:Key(0)] long JournalId);
[MessagePackObject] public sealed record GetAccountBalancesRequest;
[MessagePackObject] public sealed record GetTrialBalanceRequest;
[MessagePackObject] public sealed record GetCapacityReservationRequest([property:Key(0)] Guid ReservationId);
[MessagePackObject] public sealed record GetCapacityUsageRequest;
[MessagePackObject] public sealed record GetFundTransactionsPageRequest([property:Key(0)] int PageSize,[property:Key(1)] FinancialPageCursor? Cursor=null);
[MessagePackObject] public sealed record GetFundReservationsPageRequest([property:Key(0)] int PageSize,[property:Key(1)] FinancialPageCursor? Cursor=null);
[MessagePackObject] public sealed record GetReconciliationRequest([property:Key(0)] Guid ReconciliationId);

[MessagePackObject]
public sealed record FinancialPageCursor([property:Key(0)] int PortfolioId,[property:Key(1)] int FundId,
    [property:Key(2)] long AsOfRevision,[property:Key(3)] long AfterRevision,[property:Key(4)] int AfterOrdinal,
    [property:Key(5)] string Kind);
[MessagePackObject]
public sealed record FinancialPage<T>([property:Key(0)] T[] Items,[property:Key(1)] FinancialPageCursor? NextCursor,
    [property:Key(2)] long AsOfRevision);

[MessagePackObject]
public sealed record FinancialOperationOutcome
{
    [Key(0)] public LedgerPostingCompletedEvent? Posting { get; init; }
    [Key(1)] public LedgerPostingBatchCompletedEvent? Batch { get; init; }
    [Key(2)] public CapacityReservationCompletedEvent? Reservation { get; init; }
    [Key(3)] public CapacityConsumptionCompletedEvent? Consumption { get; init; }
    [Key(4)] public CapacityLifecycleCompletedEvent? Lifecycle { get; init; }
    [Key(5)] public LedgerConfigurationCompletedEvent? Configuration { get; init; }
    [Key(6)] public EmulatorOrderSubmittedEvent? EmulatorSubmission { get; init; }
}
[MessagePackObject]
public sealed record FinancialAccountBalance([property:Key(0)] int AccountId,[property:Key(1)] int? FundId,
    [property:Key(2)] string Currency,[property:Key(3)] decimal Debits,[property:Key(4)] decimal Credits,[property:Key(5)] decimal Balance);
[MessagePackObject]
public sealed record FinancialBalanceSnapshot([property:Key(0)] int BookId,[property:Key(1)] string OperatingState,
    [property:Key(2)] FinancialAccountBalance[] Accounts,[property:Key(3)] decimal PendingWithdrawals,
    [property:Key(4)] decimal AvailableCash,[property:Key(5)] bool MigrationQualified);
[MessagePackObject]
public sealed record FinancialTrialBalance([property:Key(0)] FinancialAccountBalance[] Accounts,[property:Key(1)] decimal TotalDebits,
    [property:Key(2)] decimal TotalCredits,[property:Key(3)] bool Balanced);
[MessagePackObject]
public sealed record FinancialJournalEntry([property:Key(0)] int Ordinal,[property:Key(1)] int AccountId,[property:Key(2)] long AccountVersion,
    [property:Key(3)] int? FundId,[property:Key(4)] decimal Debit,[property:Key(5)] decimal Credit,[property:Key(6)] string SourceLineReference);
[MessagePackObject]
public sealed record FinancialJournal([property:Key(0)] long JournalId,[property:Key(1)] long TransactionId,[property:Key(2)] int BookId,
    [property:Key(3)] int? FundId,[property:Key(4)] DateOnly AccountingDate,[property:Key(5)] string JournalHash,
    [property:Key(6)] FinancialJournalEntry[] Entries);
[MessagePackObject]
public sealed record FinancialTransactionRow([property:Key(0)] long TransactionId,[property:Key(1)] Guid OperationId,
    [property:Key(2)] long FinancialRevision,[property:Key(3)] int Ordinal,[property:Key(4)] LedgerPostingRequest Transaction,
    [property:Key(5)] long? JournalId);
[MessagePackObject]
public sealed record FinancialReservationView([property:Key(0)] CapacityReservationReceipt OriginalReceipt,
    [property:Key(1)] ReservationSnapshot Current);
[MessagePackObject]
public sealed record FinancialCapacityUsage([property:Key(0)] CapacityUsed[] Scopes);
[MessagePackObject]
public sealed record FinancialReconciliationView([property:Key(0)] Guid ReconciliationId,[property:Key(1)] string SourceCut,
    [property:Key(2)] string Status,[property:Key(3)] string ContentHash,[property:Key(4)] string DifferencesJson);
