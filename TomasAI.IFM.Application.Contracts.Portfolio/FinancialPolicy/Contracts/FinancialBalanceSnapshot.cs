using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialBalanceSnapshot([property: Key(0)] int BookId, [property: Key(1)] string OperatingState,
    [property: Key(2)] FinancialAccountBalance[] Accounts, [property: Key(3)] decimal PendingWithdrawals,
    [property: Key(4)] decimal AvailableCash, [property: Key(5)] bool MigrationQualified);
