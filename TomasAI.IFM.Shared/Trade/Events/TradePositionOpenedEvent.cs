using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePositionOpenedEvent : IEvent, ITradeDiaryEvent
    {
        [IgnoreMember] public const string Actor = "TradeEvent";
        [IgnoreMember] public const string Verb = "PositionOpened";
        [IgnoreMember] public const int ErrorCode = 9067;
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
        [Key(8)] public TradePositionEntityId TradePositionId { get; init; }
        [Key(9)] public TradePositionActionReadModel TradePositionAction { get; init; }
        [Key(10)] public DateTime CreatedOn { get; init; }
        [Key(11)] public string CreatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePositionOpenedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePositionOpenedEvent() { }

        [SerializationConstructor]
        public TradePositionOpenedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradePositionEntityId tradePositionId,
            TradePositionActionReadModel tradePositionAction,
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
            TradePositionId = tradePositionId;
            TradePositionAction = tradePositionAction;
            CreatedOn = createdOn;
            CreatedBy = createdBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradePositionOpenedCompleteEvent
        {
            CommandId = this.CommandId,
            TradePositionId = this.TradePositionId,
            TradePositionAction = this.TradePositionAction,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new TradePositionOpenedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record TradePositionOpenedCompleteEvent : CompleteEvent
    {
        public TradePositionEntityId TradePositionId { get; init; }
        public TradePositionActionReadModel TradePositionAction { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }
    }

    public record TradePositionOpenedFailEvent : ErrorEvent
    {
    }

}
