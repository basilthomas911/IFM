using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalHoldTradeChangedEvent : IEvent<FuturesItiSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalEvent";
    [IgnoreMember] public const string Verb = "HoldTradeChanged";
    [IgnoreMember] public const int ErrorCode = 19014;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesItiSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesItiSignalId FuturesItiSignalId { get; init; }
    [Key(9)] public bool HoldTrade { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }
    [Key(11)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesItiSignalHoldTradeChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesItiSignalHoldTradeChangedEvent() { }

    [SerializationConstructor]
    public FuturesItiSignalHoldTradeChangedEvent(
        ActorSubject subject,
        Guid id,
        FuturesItiSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesItiSignalId futuresItiSignalId,
        bool holdTrade,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        FuturesItiSignalId = futuresItiSignalId;
        HoldTrade = holdTrade;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
}
