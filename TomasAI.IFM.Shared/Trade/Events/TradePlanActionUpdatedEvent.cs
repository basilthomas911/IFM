using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePlanActionUpdatedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradePlanEvent";
        [IgnoreMember] public const string Verb = "ActionUpdated";
        [IgnoreMember] public const int ErrorCode = 7080;
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
        [Key(8)] public TradePlanActionReadModel TradePlanAction { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePlanActionUpdatedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePlanActionUpdatedEvent() { }

        [SerializationConstructor]
        public TradePlanActionUpdatedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradePlanActionReadModel tradePlanAction)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradePlanAction = tradePlanAction;
        }

        public ICompleteEvent ToCompletedEvent() => new TradePlanActionUpdatedCompleteEvent
        {
            CommandId = this.CommandId,
            TradePlanAction = this.TradePlanAction
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new TradePlanActionUpdatedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record TradePlanActionUpdatedCompleteEvent : CompleteEvent
    {
        public TradePlanActionReadModel TradePlanAction { get; init; }
    }

    public record TradePlanActionUpdatedFailEvent : ErrorEvent
    {
    }

}
