using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record StrikePriceVolatilityAddedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "StrikePriceVolatilityEvent";
        [IgnoreMember] public const string Verb = "Added";
        [IgnoreMember] public const int ErrorCode = 7006;
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
        [Key(8)] public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        [Key(9)] public DateTime CreatedOn { get; init; }
        [Key(10)] public string CreatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(StrikePriceVolatilityAddedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public StrikePriceVolatilityAddedEvent() { }

        [SerializationConstructor]
        public StrikePriceVolatilityAddedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            StrikePriceVolatilityReadModel strikePriceVolatility,
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
            StrikePriceVolatility = strikePriceVolatility;
            CreatedOn = createdOn;
            CreatedBy = createdBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new StrikePriceVolatilityAddedCompleteEvent
        {
            CommandId = this.CommandId,
            StrikePriceVolatility = this.StrikePriceVolatility,
            CreatedOn = this.CreatedOn,
            CreatedBy = this.CreatedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new StrikePriceVolatilityAddedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record StrikePriceVolatilityAddedCompleteEvent : CompleteEvent
    {
        public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        public DateTime CreatedOn { get; init; }
        public string CreatedBy { get; init; }

    }

    public record StrikePriceVolatilityAddedFailEvent : ErrorEvent
    {
    }

}
