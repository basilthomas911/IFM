using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePositionAddedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradePositionEvent";
        [IgnoreMember] public const string Verb = "Added";
        [IgnoreMember] public const int ErrorCode = 9060;
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
        [Key(9)] public decimal AssetPrice { get; init; }
        [Key(10)] public double RiskFreeRate { get; init; }
        [Key(11)] public TradePositionReadModel TradePosition { get; init; }
        [Key(12)] public DateTime CreatedOn { get; init; }
        [Key(13)] public string CreatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePositionAddedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePositionAddedEvent() { }

        [SerializationConstructor]
        public TradePositionAddedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            int orderId,
            decimal assetPrice,
            double riskFreeRate,
            TradePositionReadModel tradePosition,
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
            OrderId = orderId;
            AssetPrice = assetPrice;
            RiskFreeRate = riskFreeRate;
            TradePosition = tradePosition;
            CreatedOn = createdOn;
            CreatedBy = createdBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradePositionAddedCompleteEvent
        {
            CommandId = this.CommandId,
            OrderId = this.OrderId,
            AssetPrice = this.AssetPrice,
            RiskFreeRate = this.RiskFreeRate,
            TradePosition = this.TradePosition,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new TradePositionAddedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record TradePositionAddedCompleteEvent : CompleteEvent
    {
        public int OrderId { get; init; }
        public decimal AssetPrice { get; init; }
        public double RiskFreeRate { get; init; }
        public TradePositionReadModel TradePosition { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }

    }

    public record TradePositionAddedFailEvent : ErrorEvent
    {
    }


}
