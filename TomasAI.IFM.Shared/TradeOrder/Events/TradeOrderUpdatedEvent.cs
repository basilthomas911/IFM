using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.TradeOrder.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradeOrderUpdatedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradeOrderEvent";
        [IgnoreMember] public const string Verb = "Updated";
        [IgnoreMember] public const int ErrorCode = 7042;
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
        [Key(9)] public decimal OrderPrice { get; init; }
        [Key(10)] public DateTime UpdatedOn { get; init; }
        [Key(11)] public string UpdatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradeOrderUpdatedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeOrderUpdatedEvent() { }

        [SerializationConstructor]
        public TradeOrderUpdatedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradeOrderEntityId tradeOrderId,
            decimal orderPrice,
            DateTime updatedOn,
            string updatedBy)
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
            OrderPrice = orderPrice;
            UpdatedOn = updatedOn;
            UpdatedBy = updatedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradeOrderUpdatedCompleteEvent
        {
            TradeOrderId = TradeOrderId,
            OrderPrice = OrderPrice,
            UpdatedOn = UpdatedOn,
            UpdatedBy = UpdatedBy
        }.RoutedFrom(this);
        public IErrorEvent ToFailedEvent(Exception ex) => new TradeOrderUpdatedFailEvent
        {
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        }.RoutedFrom(this);
    }


    public record TradeOrderUpdatedCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public decimal OrderPrice { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }
    }

    public record TradeOrderUpdatedFailEvent : ErrorEvent
    {
    }
   
}
