using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one replayable VWAP session-state transition.</summary>
[MessagePackObject]
public sealed record FuturesVwapSignalUpdatedEvent : IEvent<FuturesVwapSignalEntityId>
{
    public const string Actor = "FuturesVwapSignalEvent";
    public const string Verb = "SignalUpdated";
    public const int ErrorCode = 26410;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesVwapCheckpoint FuturesVwapCheckpoint { get; init; } = new();
    [Key(9)] public FuturesVwapSignalReadModel FuturesVwapSignal { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesVwapSignalUpdatedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesVwapSignalUpdatedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesVwapSignalUpdatedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            FuturesVwapCheckpoint = FuturesVwapCheckpoint,
            FuturesVwapSignal = FuturesVwapSignal
        };

    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return (IErrorEvent<TEntityId>)(object)FuturesVwapSignalUpdatedFailEvent.Create(this, ex);
    }
}
