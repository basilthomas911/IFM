using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.TradeOrder.Events;

[MessagePackObject(AllowPrivate = true)]
public record TradeOrderOpenedEvent : IEvent
{
    [IgnoreMember] public const string Actor = "TradeOrderEvent";
    [IgnoreMember] public const string Verb = "Opened";
    [IgnoreMember] public const int ErrorCode = 7040;
    [IgnoreMember] static readonly string CachedUserName = $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public string EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public TradeOrderEntityId TradeOrderId { get; init; }
    [Key(9)] public DateTime OpenedOn { get; init; }
    [Key(10)] public string? OpenedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(TradeOrderOpenedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradeOrderOpenedEvent() { }

    [SerializationConstructor]
    public TradeOrderOpenedEvent(
        ActorSubject subject,
        Guid id,
        string entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        TradeOrderEntityId tradeOrderId,
        DateTime openedOn,
        string? openedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        TradeOrderId = tradeOrderId;
        OpenedOn = openedOn;
        OpenedBy = openedBy ?? string.Empty;
    }

    public ICompleteEvent ToCompletedEvent() => new TradeOrderOpenedCompleteEvent
    {
        TradeOrderId = TradeOrderId,
        OpenedOn = OpenedOn,
        OpenedBy = OpenedBy
    }.RoutedFrom(this);
    public IErrorEvent ToFailedEvent(Exception ex) => new TradeOrderOpenedFailEvent
    {
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    }.RoutedFrom(this);
}


public record TradeOrderOpenedCompleteEvent : CompleteEvent
{
    public TradeOrderEntityId TradeOrderId { get; init; }
    public DateTime OpenedOn { get; init; }
    public string OpenedBy { get; init; }
}

public record TradeOrderOpenedFailEvent : ErrorEvent
{
}
