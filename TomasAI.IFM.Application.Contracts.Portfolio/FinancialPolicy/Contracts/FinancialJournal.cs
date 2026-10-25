using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialJournal([property: Key(0)] long JournalId, [property: Key(1)] long TransactionId, [property: Key(2)] int BookId,
    [property: Key(3)] int? FundId, [property: Key(4)] DateOnly AccountingDate, [property: Key(5)] string JournalHash,
    [property: Key(6)] FinancialJournalEntry[] Entries);
