using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record StrikePriceVolatilityChangedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "StrikePriceVolatilityEvent";
        [IgnoreMember] public const string Verb = "Changed";
        [IgnoreMember] public const int ErrorCode = 7007;
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
        [Key(8)] public StrikePriceVolatilityId OriginalId { get; init; }
        [Key(9)] public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        [Key(10)] public DateTime UpdatedOn { get; init; }
        [Key(11)] public string UpdatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(StrikePriceVolatilityChangedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public StrikePriceVolatilityChangedEvent() { }

        [SerializationConstructor]
        public StrikePriceVolatilityChangedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            StrikePriceVolatilityId originalId,
            StrikePriceVolatilityReadModel strikePriceVolatility,
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
            OriginalId = originalId;
            StrikePriceVolatility = strikePriceVolatility;
            UpdatedOn = updatedOn;
            UpdatedBy = updatedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new StrikePriceVolatilityChangedCompleteEvent
        {
            CommandId = this.CommandId,
            OriginalId = this.OriginalId,
            StrikePriceVolatility = this.StrikePriceVolatility,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new StrikePriceVolatilityChangedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record StrikePriceVolatilityChangedCompleteEvent : CompleteEvent
    {
        public StrikePriceVolatilityId OriginalId { get; init; }
        public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }

    }

    public record StrikePriceVolatilityChangedFailEvent : ErrorEvent
    {
    }

}
