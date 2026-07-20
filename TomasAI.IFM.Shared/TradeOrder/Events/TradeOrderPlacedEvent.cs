using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Shared.TradeOrder.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradeOrderPlacedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradeEvent";
        [IgnoreMember] public const string Verb = "OrderPlaced";
        [IgnoreMember] public const int ErrorCode = 7041;
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
        [Key(8)] public TradeOrderReadModel TradeOrder { get; init; }
        [Key(9)] public DateTime SubmittedOn { get; init; }
        [Key(10)] public string SubmittedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradeOrderPlacedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeOrderPlacedEvent() { }

        [SerializationConstructor]
        public TradeOrderPlacedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradeOrderReadModel tradeOrder,
            DateTime submittedOn,
            string submittedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradeOrder = tradeOrder;
            SubmittedOn = submittedOn;
            SubmittedBy = submittedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradeOrderPlacedCompleteEvent
        {
            TradeOrder = TradeOrder,
            SubmittedOn = SubmittedOn,
            SubmittedBy = SubmittedBy
        }.RoutedFrom(this);
        public IErrorEvent ToFailedEvent(Exception ex) => new TradeOrderPlacedFailEvent
        {
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        }.RoutedFrom(this);
    }


    public record TradeOrderPlacedCompleteEvent : CompleteEvent
    {
        public TradeOrderReadModel TradeOrder { get; init; }
        public DateTime SubmittedOn { get; init; }
        public string SubmittedBy { get; init; }

    }

    public record TradeOrderPlacedFailEvent : ErrorEvent
    {
    }

}
