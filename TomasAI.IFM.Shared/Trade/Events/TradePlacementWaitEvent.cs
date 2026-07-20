using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events;

[MessagePackObject(AllowPrivate = true)]
public record TradePlacementWaitEvent : IEvent
{
    [IgnoreMember] public const string Actor = "TradePlacementEvent";
    [IgnoreMember] public const string Verb = "Wait";
    [IgnoreMember] public const int ErrorCode = 11066;
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
    [Key(8)] public TradePlacementId TradePlacementId { get; init; }
    [Key(9)] public FuturesTradeSignalV2ReadModel FuturesTradeSignal { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }
    [Key(11)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => CachedUserName;
    [IgnoreMember] public string EventName => nameof(TradePlacementWaitEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public TradePlacementWaitEvent() { }

    [SerializationConstructor]
    public TradePlacementWaitEvent(
        ActorSubject subject,
        Guid id,
        string entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        TradePlacementId tradePlacementId,
        FuturesTradeSignalV2ReadModel futuresTradeSignal,
        DateTime createdOn,
        string createdBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        TradePlacementId = tradePlacementId;
        FuturesTradeSignal = futuresTradeSignal;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    public ICompleteEvent ToCompletedEvent() => new TradePlacementWaitCompleteEvent
    {
        TradePlacementId = this.TradePlacementId,
        FuturesTradeSignal = this.FuturesTradeSignal,
        CreatedOn = this.CreatedOn,
        CreatedBy = this.CreatedBy,
    };
    public IErrorEvent ToFailedEvent(Exception ex) => new TradePlacementWaitFailEvent
    {
        CommandId = this.CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };
}


public record TradePlacementWaitCompleteEvent : CompleteEvent
{
    public TradePlacementId TradePlacementId { get; init; }
    public FuturesTradeSignalV2ReadModel FuturesTradeSignal { get; init; }
    public DateTime CreatedOn { get; init; }
    public string CreatedBy { get; init; }
}

public record TradePlacementWaitFailEvent : ErrorEvent
{
}
