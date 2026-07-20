using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePlanForwardLossLimitWarningUpdatedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradePlanEvent";
        [IgnoreMember] public const string Verb = "ForwardLossLimitWarningUpdated";
        [IgnoreMember] public const int ErrorCode = 7052;
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
        [Key(8)] public TradePlanForwardLossLimitReadModel TradePlanForwardLossLimit { get; init; }
        [Key(9)] public DateTime UpdatedOn { get; init; }
        [Key(10)] public string UpdatedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePlanForwardLossLimitWarningUpdatedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePlanForwardLossLimitWarningUpdatedEvent() { }

        [SerializationConstructor]
        public TradePlanForwardLossLimitWarningUpdatedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit,
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
            TradePlanForwardLossLimit = tradePlanForwardLossLimit;
            UpdatedOn = updatedOn;
            UpdatedBy = updatedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradePlanForwardLossLimitWarningUpdatedCompleteEvent
        {
            TradePlanForwardLossLimit = this.TradePlanForwardLossLimit,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new TradePlanForwardLossLimitWarningUpdatedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record TradePlanForwardLossLimitWarningUpdatedCompleteEvent : CompleteEvent
    {
        public TradePlanForwardLossLimitReadModel TradePlanForwardLossLimit { get; init; }
        public DateTime UpdatedOn { get; init; }
        public string UpdatedBy { get; init; }

    }

    public record TradePlanForwardLossLimitWarningUpdatedFailEvent : ErrorEvent
    {
    }
}
