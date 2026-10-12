using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialAccountBalance([property: Key(0)] int AccountId, [property: Key(1)] int? FundId,
    [property: Key(2)] string Currency, [property: Key(3)] decimal Debits, [property: Key(4)] decimal Credits, [property: Key(5)] decimal Balance);
