using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialTransactionRow([property: Key(0)] long TransactionId, [property: Key(1)] Guid OperationId,
    [property: Key(2)] long FinancialRevision, [property: Key(3)] int Ordinal, [property: Key(4)] LedgerPostingRequest Transaction,
    [property: Key(5)] long? JournalId);
