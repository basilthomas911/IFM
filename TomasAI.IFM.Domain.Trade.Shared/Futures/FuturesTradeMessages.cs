using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures;

public static class FuturesTradeActorNames
{
    public const string Command = "FuturesTradeCommand";
    public const string Query = "FuturesTradeQuery";
}

[MessagePackObject]
public sealed record CreateFuturesTradeCommand : CreateEstablishedTradeCommand
{ public const string Verb="CreateFuturesTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }
[MessagePackObject]
public sealed record AmendFuturesTradeEvidenceCommand : AmendEstablishedTradeEvidenceCommand
{ public const string Verb="AmendFuturesTradeEvidence"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }
[MessagePackObject]
public sealed record BeginCloseFuturesTradeCommand : EstablishedTradeCommand
{ public const string Verb="BeginCloseFuturesTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }
[MessagePackObject]
public sealed record CloseFuturesTradeCommand : EstablishedTradeCommand
{ public const string Verb="CloseFuturesTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.FuturesTradeBoundedContext; }

[MessagePackObject]
public sealed record FuturesTradeChangedEvent : IEvent<TradeEntityId>
{
    public const string Verb = "FuturesTradeChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TradeEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public EstablishedTradeDefinition State { get; init; } = new();
    [Key(9)] public bool IsInitialEstablishment { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTradeChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
