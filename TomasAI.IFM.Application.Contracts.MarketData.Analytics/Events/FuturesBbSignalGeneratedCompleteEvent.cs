using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Reports successful Bollinger projection.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesBbSignalGeneratedCompleteEvent : ICompleteEvent<FuturesTradeSessionBarEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesBbSignalGeneratedCompleteEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="futuresBbSignal">The FuturesBbSignal field.</param>
    /// <param name="futuresBbCheckpoint">The FuturesBbCheckpoint field.</param>
    [SerializationConstructor]
    public FuturesBbSignalGeneratedCompleteEvent(ActorSubject subject, FuturesTradeSessionBarEntityId entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, FuturesBbSignalReadModel futuresBbSignal, FuturesBbAccumulatorCheckpoint futuresBbCheckpoint)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesBbSignal = futuresBbSignal;
        FuturesBbCheckpoint = futuresBbCheckpoint;
    }
    public const string Verb = "SignalGeneratedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesTradeSessionBarEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesBbSignalReadModel FuturesBbSignal { get; init; } = new();
    [Key(9)] public FuturesBbAccumulatorCheckpoint FuturesBbCheckpoint { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesBbSignalGeneratedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}
