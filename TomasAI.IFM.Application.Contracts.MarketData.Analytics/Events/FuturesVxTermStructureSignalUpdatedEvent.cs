using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one accepted VX curve leg and the resulting paired futuresVxTermStructureCheckpoint.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureSignalUpdatedEvent
    : IEvent<FuturesVxTermStructureSignalEntityId>
{
    public const string Actor = "FuturesVxTermStructureSignalEvent";
    public const string Verb = "SignalUpdated";
    public const int ErrorCode = 26301;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesVxTermStructureSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesVxTermStructureCheckpoint FuturesVxTermStructureCheckpoint { get; init; } = new();
    [Key(9)] public FuturesVxTermStructureSignalReadModel? FuturesVxTermStructureSignal { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesVxTermStructureSignalUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesVxTermStructureSignalUpdatedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesVxTermStructureSignalUpdatedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            FuturesVxTermStructureCheckpoint = FuturesVxTermStructureCheckpoint,
            FuturesVxTermStructureSignal = FuturesVxTermStructureSignal
        };

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return (IErrorEvent<TEntityId>)(object)FuturesVxTermStructureSignalUpdatedFailEvent.Create(this, ex);
    }
}
