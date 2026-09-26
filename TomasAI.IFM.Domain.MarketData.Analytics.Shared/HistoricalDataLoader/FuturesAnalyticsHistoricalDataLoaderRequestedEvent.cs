using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Records that a validated data load request entered durable processing.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesAnalyticsHistoricalDataLoaderRequestedEvent
    : IEvent<FuturesAnalyticsHistoricalDataLoaderEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesAnalyticsHistoricalDataLoaderRequestedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="parameters">The Parameters field.</param>
    [SerializationConstructor]
    public FuturesAnalyticsHistoricalDataLoaderRequestedEvent(ActorSubject subject, Guid id, FuturesAnalyticsHistoricalDataLoaderEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, FuturesAnalyticsHistoricalDataLoaderParameters parameters)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        Parameters = parameters;
    }
    /// <summary>Gets the durable Event actor name.</summary>
    public const string Actor = "FuturesAnalyticsHistoricalDataLoaderEvent";
    /// <summary>Gets the event verb.</summary>
    public const string Verb = "Requested";
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
    /// <summary>Gets the immutable data load parameters.</summary>
    [Key(8)] public FuturesAnalyticsHistoricalDataLoaderParameters Parameters { get; init; } = new();
    /// <inheritdoc />
    [IgnoreMember] public string UserName => Parameters.RequestedBy;
    /// <inheritdoc />
    [IgnoreMember] public string EventName => nameof(FuturesAnalyticsHistoricalDataLoaderRequestedEvent);
    /// <inheritdoc />
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
