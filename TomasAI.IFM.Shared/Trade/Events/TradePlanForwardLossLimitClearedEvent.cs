using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Trade.Events
{

    [MessagePackObject(AllowPrivate = true)]
    public record TradePlanForwardLossLimitClearedEvent : IEvent
    {
        [IgnoreMember] public const string Actor = "TradePlanEvent";
        [IgnoreMember] public const string Verb = "ForwardLossLimitCleared";
        [IgnoreMember] public const int ErrorCode = 7050;
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
        [Key(8)] public TradePlanForwardLossLimitEntityId ForwardLossLimitId { get; init; }
        [Key(9)] public DateTime ClearedOn { get; init; }
        [Key(10)] public string ClearedBy { get; init; }

        [IgnoreMember] public string UserName => CachedUserName;
        [IgnoreMember] public string EventName => nameof(TradePlanForwardLossLimitClearedEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradePlanForwardLossLimitClearedEvent() { }

        [SerializationConstructor]
        public TradePlanForwardLossLimitClearedEvent(
            ActorSubject subject,
            Guid id,
            string entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            TradePlanForwardLossLimitEntityId forwardLossLimitId,
            DateTime clearedOn,
            string clearedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            ForwardLossLimitId = forwardLossLimitId;
            ClearedOn = clearedOn;
            ClearedBy = clearedBy ?? string.Empty;
        }

        public ICompleteEvent ToCompletedEvent() => new TradePlanForwardLossLimitClearedCompleteEvent
        {
            ForwardLossLimitId = this.ForwardLossLimitId,
            ClearedOn = this.ClearedOn,
            ClearedBy = this.ClearedBy
        };
        public IErrorEvent ToFailedEvent(Exception ex) => new TradePlanForwardLossLimitClearedFailEvent
        {
            CommandId = this.CommandId,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode
        };
    }


    public record TradePlanForwardLossLimitClearedCompleteEvent : CompleteEvent
    {
        public TradePlanForwardLossLimitEntityId ForwardLossLimitId { get; init; }
        public DateTime ClearedOn { get; init; }
        public string ClearedBy { get; init; }

    }

    public record TradePlanForwardLossLimitClearedFailEvent : ErrorEvent
    {
    }
}
