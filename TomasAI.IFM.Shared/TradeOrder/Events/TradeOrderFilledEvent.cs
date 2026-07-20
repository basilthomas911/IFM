using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.TradeOrder.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradeOrderFilledEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradeOrderEvent";
        [IgnoreMember] public const string Verb = "Filled";
        [IgnoreMember] public const int ErrorCode = 7039;
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
        [Key(9)] public TradeFillReadModel? TradeFill { get; init; }
        [Key(10)] public TradeOrderState TradeOrderState { get; init; }
        [Key(11)] public DateTime FilledOn { get; init; }
        [Key(12)] public string? FilledBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradeOrderFilledEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeOrderFilledEvent() { }

        [SerializationConstructor]
        public TradeOrderFilledEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradeOrderEntityId tradeOrderId,
            TradeFillReadModel? tradeFill,
            TradeOrderState tradeOrderState,
            DateTime filledOn,
            string? filledBy)
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
            TradeFill = tradeFill;
            TradeOrderState = tradeOrderState;
            FilledOn = filledOn;
            FilledBy = filledBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradeOrderFilledCompleteEvent
        {
            TradeOrderId = TradeOrderId,
            TradeFill = TradeFill,
            TradeOrderState = TradeOrderState,
            FilledOn = FilledOn,
            FilledBy = FilledBy
        }.RoutedFrom(this);
        public IErrorEvent ToFailedEvent(Exception ex) => new TradeOrderFilledFailEvent
        {
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        }.RoutedFrom(this);
    }


    public record TradeOrderFilledCompleteEvent : CompleteEvent
    {
        public TradeOrderEntityId TradeOrderId { get; init; }
        public TradeFillReadModel TradeFill { get; init; }
        public TradeOrderState TradeOrderState { get; init; }
        public DateTime FilledOn { get; init; }
        public string FilledBy { get; init; }
    }

    public record TradeOrderFilledFailEvent : ErrorEvent
    {
    }

}
