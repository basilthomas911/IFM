using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Reports one terminal data load failure and its last durable checkpoint.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesAnalyticsHistoricalDataLoaderFailedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesAnalyticsHistoricalDataLoaderFailedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="errorMessage">The ErrorMessage field.</param>
    /// <param name="lastCompletedBatchOrdinal">The LastCompletedBatchOrdinal field.</param>
    /// <param name="lastCompletedRecordOrdinal">The LastCompletedRecordOrdinal field.</param>
    [SerializationConstructor]
    public FuturesAnalyticsHistoricalDataLoaderFailedEvent(ActorSubject subject, Guid id, FuturesAnalyticsHistoricalDataLoaderEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, string errorMessage, int lastCompletedBatchOrdinal, long lastCompletedRecordOrdinal)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        ErrorMessage = errorMessage;
        LastCompletedBatchOrdinal = lastCompletedBatchOrdinal;
        LastCompletedRecordOrdinal = lastCompletedRecordOrdinal;
    }
    /// <summary>Gets the Event actor name.</summary>
    public const string Actor = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Failed";
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public Guid Id { get; init; }
    /// <inheritdoc />
    [Key(2)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    /// <inheritdoc />
    [Key(3)] public long EventId { get; init; }
    /// <inheritdoc />
    [Key(4)] public Guid CommandId { get; init; }
    /// <inheritdoc />
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    /// <inheritdoc />
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the sanitized terminal failure text.</summary>
    [Key(8)] public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Gets the last completely persisted batch ordinal.</summary>
    [Key(9)] public int LastCompletedBatchOrdinal { get; init; }
    /// <summary>Gets the last completely persisted record ordinal.</summary>
    [Key(10)] public long LastCompletedRecordOrdinal { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public string UserName => string.Empty;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderFailedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
