using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Event published when futures bar data has been deleted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataDeletedCompleteEvent : ICompleteEvent<FuturesBarDataId>
{
    [IgnoreMember] public const string Actor = "FuturesBarDataEvent";
    [IgnoreMember] public const string Verb = "DeletedComplete";
    [IgnoreMember] static readonly string CachedUserName = string.Concat(Environment.UserDomainName, "\\", Environment.UserName);

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesBarDataId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; } = Guid.NewGuid();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesBarDataId BarDataId { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesBarDataDeletedCompleteEvent);
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesBarDataDeletedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesBarDataDeletedCompleteEvent(
        ActorSubject subject,
        FuturesBarDataId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesBarDataId barDataId,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        BarDataId = barDataId;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }
}
