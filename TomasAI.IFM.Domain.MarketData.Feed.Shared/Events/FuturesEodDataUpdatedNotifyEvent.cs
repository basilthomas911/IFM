using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

/// <summary>
/// Provides external observers with the latest successfully persisted futures EOD display snapshot.
/// </summary>
/// <remarks>
/// This is a best-effort Core NATS notification. It is emitted only after
/// <see cref="FuturesEodDataInsertedCompleteEvent"/> and is not part of the durable insert workflow.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesEodDataUpdatedNotifyEvent : IEvent<FuturesEodDataId>
{
    [IgnoreMember] public const string Actor = "FuturesEodDataNotification";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesEodDataId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesEodDataUpdatedNotifyEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool IsValid => CommandId != Guid.Empty && FuturesEodData.IsValid;

    public FuturesEodDataUpdatedNotifyEvent() { }

    [SerializationConstructor]
    public FuturesEodDataUpdatedNotifyEvent(
        ActorSubject subject,
        Guid id,
        FuturesEodDataId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesEodDataV2ReadModel futuresEodData)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesEodData = futuresEodData;
    }
}
