using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalMDIWatermarkClearedEvent : IEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalEvent";
    [IgnoreMember] public const string Verb = "MDIWatermarkCleared";
    [IgnoreMember] public const int ErrorCode = 19012;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesTradeSignalId FuturesTradeSignalId { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalMDIWatermarkClearedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesTradeSignalMDIWatermarkClearedEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalMDIWatermarkClearedEvent(
        ActorSubject subject,
        Guid id,
        FuturesTradeSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesTradeSignalId futuresTradeSignalId,
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
        FuturesTradeSignalId = futuresTradeSignalId;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
}
