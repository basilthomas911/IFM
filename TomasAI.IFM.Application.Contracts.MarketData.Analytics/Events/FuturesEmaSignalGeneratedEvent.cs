using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records one event-sourced EMA state transition.</summary>
[MessagePackObject]
public sealed record FuturesEmaSignalGeneratedEvent : IEvent<FuturesTradeSessionBarEntityId>
{
    public const string Actor = "FuturesEmaSignalEvent";
    public const string Verb = "SignalGenerated";
    public const int ErrorCode = 26101;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the generated EMA family.</summary>
    [Key(8)] public FuturesEmaSignalReadModel FuturesEmaSignal { get; init; } = new();
    /// <summary>Gets the immutable source observation for downstream BB composition.</summary>
    [Key(9)] public FuturesTradeSessionBarReadModel Observation { get; init; } = new();
    /// <summary>Gets the replayable EMA accumulator futuresEmaCheckpoint.</summary>
    [Key(10)] public FuturesEmaAccumulatorCheckpoint FuturesEmaCheckpoint { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesEmaSignalGeneratedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <inheritdoc />
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId> where TEntityId : IActorEntityId =>
        (ICompleteEvent<TEntityId>)(object)new FuturesEmaSignalGeneratedCompleteEvent
        {
            Subject = new(ActorType.Event, Actor, FuturesEmaSignalGeneratedCompleteEvent.Verb, EntityId.Format()),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            FuturesEmaSignal = FuturesEmaSignal,
            Observation = Observation,
            FuturesEmaCheckpoint = FuturesEmaCheckpoint
        };

    /// <inheritdoc />
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId> where TEntityId : IActorEntityId 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return (IErrorEvent<TEntityId>)(object)FuturesEmaSignalGeneratedFailEvent.Create(this, ex);
    }
}
