using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Events;

[MessagePackObject(AllowPrivate = true)]
public record FuturesTradeSignalLLMStartedEvent : IEvent<FuturesTradeSignalEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalEvent";
    [IgnoreMember] public const string Verb = "LLMStarted";
    [IgnoreMember] public const int ErrorCode = 19010;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesTradeSignalEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime StartedOn { get; init; }
    [Key(9)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(FuturesTradeSignalLLMStartedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesTradeSignalLLMStartedEvent() { }

    [SerializationConstructor]
    public FuturesTradeSignalLLMStartedEvent(
        ActorSubject subject,
        Guid id,
        FuturesTradeSignalEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime startedOn,
        string startedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        StartedOn = startedOn;
        StartedBy = startedBy;
    }
}
