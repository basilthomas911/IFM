using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.TradeOrder.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradeOrderCancelledEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradeOrderEvent";
        [IgnoreMember] public const string Verb = "Cancelled";
        [IgnoreMember] public const int ErrorCode = 7037;
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
        [Key(9)] public DateTime CancelledOn { get; init; }
        [Key(10)] public string CancelledBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradeOrderCancelledEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeOrderCancelledEvent() { }

        [SerializationConstructor]
        public TradeOrderCancelledEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradeOrderEntityId tradeOrderId,
            DateTime cancelledOn,
            string cancelledBy)
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
            CancelledOn = cancelledOn;
            CancelledBy = cancelledBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradeOrderCancelledCompleteEvent
        {
            TradeOrderId = TradeOrderId,
            CancelledOn = CancelledOn,
            CancelledBy = CancelledBy
        }.RoutedFrom(this);
        public IErrorEvent ToFailedEvent(Exception ex) => new TradeOrderCancelledFailEvent
        {
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        }.RoutedFrom(this);
    }


    public record TradeOrderCancelledCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public DateTime CancelledOn { get; init; }
        public string CancelledBy { get; init; }
    }

    public record TradeOrderCancelledFailEvent : ErrorEvent
    {
    }

}
