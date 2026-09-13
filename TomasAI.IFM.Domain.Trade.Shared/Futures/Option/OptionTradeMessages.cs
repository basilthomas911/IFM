using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option;

public static class FuturesOptionTradeActorNames
{
    public const string Command = "FuturesOptionTradeCommand";
    public const string Query = "FuturesOptionTradeQuery";
}

[MessagePackObject]
public sealed record CreateOptionTradeCommand : CreateEstablishedTradeCommand
{ public const string Verb="CreateOptionTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }
[MessagePackObject]
public sealed record AmendOptionTradeEvidenceCommand : AmendEstablishedTradeEvidenceCommand
{ public const string Verb="AmendOptionTradeEvidence"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }
[MessagePackObject]
public sealed record BeginCloseOptionTradeCommand : EstablishedTradeCommand
{ public const string Verb="BeginCloseOptionTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }
[MessagePackObject]
public sealed record CloseOptionTradeCommand : EstablishedTradeCommand
{ public const string Verb="CloseOptionTrade"; [IgnoreMember] public override BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext; }

[MessagePackObject]
public sealed record OptionTradeChangedEvent : IEvent<TradeEntityId>
{
    public const string Verb = "OptionTradeChanged";
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
    [IgnoreMember] public string EventName => nameof(OptionTradeChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
