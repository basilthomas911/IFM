using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialJournalEntry([property: Key(0)] int Ordinal, [property: Key(1)] int AccountId, [property: Key(2)] long AccountVersion,
    [property: Key(3)] int? FundId, [property: Key(4)] decimal Debit, [property: Key(5)] decimal Credit, [property: Key(6)] string SourceLineReference);
