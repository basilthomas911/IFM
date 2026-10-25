using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when a streaming request ID has been deleted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record StreamingRequestIdDeletedCompleteEvent : ICompleteEvent<FeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "StreamingRequestIdDeletedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FeedId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FeedId FeedId { get; init; }
    [Key(9)] public DateTime DeletedOn { get; init; }
    [Key(10)] public string DeletedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public StreamingRequestIdDeletedCompleteEvent() { }

    [SerializationConstructor]
    public StreamingRequestIdDeletedCompleteEvent(
        ActorSubject subject,
        FeedId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FeedId feedId,
        DateTime deletedOn,
        string deletedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FeedId = feedId;
        DeletedOn = deletedOn;
        DeletedBy = deletedBy ?? string.Empty;
    }
}
