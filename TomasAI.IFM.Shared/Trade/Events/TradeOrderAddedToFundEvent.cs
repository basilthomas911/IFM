using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradeOrderAddedToFundEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradeOrderEvent";
        [IgnoreMember] public const string Verb = "AddedToFund";
        [IgnoreMember] public const int ErrorCode = 0;
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
        [Key(8)] public int OrderId { get; init; }
        [Key(9)] public int FundId { get; init; }
        [Key(10)] public DateTime OrderDate { get; init; }
        [Key(11)] public OrderStatus OrderStatus { get; init; }
        [Key(12)] public string Reference { get; init; }
        [Key(13)] public DateTime CreatedOn { get; init; }
        [Key(14)] public string CreatedBy { get; init; }
        [Key(15)] public DateTime UpdatedOn { get; init; }
        [Key(16)] public string UpdatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradeOrderAddedToFundEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeOrderAddedToFundEvent() { }

        [SerializationConstructor]
        public TradeOrderAddedToFundEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            int orderId,
            int fundId,
            DateTime orderDate,
            OrderStatus orderStatus,
            string reference,
            DateTime createdOn,
            string createdBy,
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
            OrderId = orderId;
            FundId = fundId;
            OrderDate = orderDate;
            OrderStatus = orderStatus;
            Reference = reference ?? string.Empty;
            CreatedOn = createdOn;
            CreatedBy = createdBy ?? string.Empty;
            UpdatedOn = updatedOn;
            UpdatedBy = updatedBy ?? string.Empty;
        }
    }

}
