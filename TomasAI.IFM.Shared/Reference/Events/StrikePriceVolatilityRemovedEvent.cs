using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference;

namespace TomasAI.IFM.Shared.Reference.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record StrikePriceVolatilityRemovedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "StrikePriceVolatilityEvent";
        [IgnoreMember] public const string Verb = "Removed";
        [IgnoreMember] public const int ErrorCode = 7008;
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
        [Key(8)] public StrikePriceVolatilityId StrikePriceVolatilityId { get; init; }
        [Key(9)] public DateTime DeletedOn { get; init; }
        [Key(10)] public string DeletedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(StrikePriceVolatilityRemovedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public StrikePriceVolatilityRemovedEvent() { }

        [SerializationConstructor]
        public StrikePriceVolatilityRemovedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            StrikePriceVolatilityId strikePriceVolatilityId,
            DateTime deletedOn,
            string deletedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            StrikePriceVolatilityId = strikePriceVolatilityId;
            DeletedOn = deletedOn;
            DeletedBy = deletedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new StrikePriceVolatilityRemovedCompleteEvent
        {
            CommandId = this.CommandId,
            StrikePriceVolatilityId = this.StrikePriceVolatilityId,
            DeletedOn = this.DeletedOn,
            DeletedBy = this.DeletedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new StrikePriceVolatilityRemovedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record StrikePriceVolatilityRemovedCompleteEvent : CompleteEvent
    {
        public StrikePriceVolatilityId StrikePriceVolatilityId { get; init; }
        public DateTime DeletedOn { get; init; }
        public string DeletedBy { get; init; }

    }

    public record StrikePriceVolatilityRemovedFailEvent : ErrorEvent
    {
    }

}
