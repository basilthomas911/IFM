using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Notifies external observers about one successfully persisted Futures ITI signal change.
/// </summary>
/// <remarks>
/// This best-effort Core NATS notification is emitted only after the durable or realtime
/// ITI projection has completed. The persisted signal remains authoritative if notification
/// delivery is temporarily unavailable.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesItiSignalUpdatedNotifyEvent : IEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalNotification";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesItiSignalV2ReadModel FuturesItiSignal { get; init; } = new();
    [Key(9)] public Guid SourceEventId { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesItiSignalUpdatedNotifyEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool IsValid =>
        CommandId != Guid.Empty && FuturesItiSignal is { IsValid: true };

    public FuturesItiSignalUpdatedNotifyEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalUpdatedNotifyEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalV2ReadModel futuresItiSignal,
        Guid sourceEventId)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesItiSignal = futuresItiSignal;
        SourceEventId = sourceEventId;
    }
}
