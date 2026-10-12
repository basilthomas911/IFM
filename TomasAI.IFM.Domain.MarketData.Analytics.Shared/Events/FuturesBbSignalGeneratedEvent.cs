using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one event-sourced Bollinger state transition.</summary>
[MessagePackObject]
public sealed record FuturesBbSignalGeneratedEvent : IEvent<FuturesTradeSessionBarEntityId>
{
    public const string Actor = "FuturesBbSignalEvent";
    public const string Verb = "SignalGenerated";
    public const int ErrorCode = 26111;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the generated Bollinger futuresBbSignal.</summary>
    [Key(8)] public FuturesBbSignalReadModel FuturesBbSignal { get; init; } = new();
    /// <summary>Gets the replayable Bollinger futuresBbCheckpoint.</summary>
    [Key(9)] public FuturesBbAccumulatorCheckpoint FuturesBbCheckpoint { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesBbSignalGeneratedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <inheritdoc />
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesBbSignalGeneratedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesBbSignalGeneratedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            FuturesBbSignal = FuturesBbSignal,
            FuturesBbCheckpoint = FuturesBbCheckpoint
        };

    /// <inheritdoc />
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return (IErrorEvent<TEntityId>)(object)FuturesBbSignalGeneratedFailEvent.Create(this, ex);
    }
}
