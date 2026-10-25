using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>
/// Provides external observers with the latest successfully persisted futures trade-signal snapshot.
/// </summary>
/// <remarks>
/// This is a best-effort Core NATS notification emitted only after
/// <see cref="FuturesTradeSignalUpdatedCompleteEvent"/>. It is not part of the durable update workflow.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesTradeSignalUpdatedNotifyEvent : IEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalNotification";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public FuturesTradeSignalV2ReadModel FuturesTradeSignal { get; init; } = new();

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalUpdatedNotifyEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    [IgnoreMember] public bool IsValid => CommandId != Guid.Empty && FuturesTradeSignal.IsValid;

    public FuturesTradeSignalUpdatedNotifyEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalUpdatedNotifyEvent(
        ActorSubject subject,
        Guid id,
        FuturesTradeSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesTradeSignal = futuresTradeSignal;
    }
}
