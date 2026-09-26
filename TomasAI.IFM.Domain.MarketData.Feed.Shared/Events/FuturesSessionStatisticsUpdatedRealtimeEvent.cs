using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Non-durable provider-neutral input carrying the latest complete session statistics.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesSessionStatisticsUpdatedRealtimeEvent : IEvent<FuturesEodDataId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesSessionStatisticsUpdatedRealtimeEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="statistics">The Statistics field.</param>
    [SerializationConstructor]
    public FuturesSessionStatisticsUpdatedRealtimeEvent(ActorSubject subject, Guid id, FuturesEodDataId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, FuturesSessionStatisticsSnapshot statistics)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        Statistics = statistics;
    }
    public const string Actor = FuturesTickTradeDataInsertedEvent.Actor;
    public const string Verb = "SessionStatisticsObserved";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesEodDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesSessionStatisticsSnapshot Statistics { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesSessionStatisticsUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
