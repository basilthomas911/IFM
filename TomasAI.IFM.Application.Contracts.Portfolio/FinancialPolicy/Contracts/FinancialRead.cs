using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialRead<T>([property: Key(0)] FinancialReadStatus Status, [property: Key(1)] T? Value,
    [property: Key(2)] long FinancialRevision, [property: Key(3)] DateTime ObservedAtUtc) where T : class;
