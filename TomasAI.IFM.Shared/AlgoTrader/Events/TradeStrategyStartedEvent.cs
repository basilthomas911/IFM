using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder;

namespace TomasAI.IFM.Shared.AlgoTrader.Events
{
    /// <summary>
    /// Event emitted when a trade strategy has been started for a specific trade order.
    /// and update read models.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    public record TradeStrategyStartedEvent : IEvent<TradeOrderEntityId>
    {
        [IgnoreMember] public const string Actor = "TradeStrategy";
        [IgnoreMember] public const string Verb = "Started";
        [IgnoreMember] public const string StartedComplete = "StartedComplete";
        [IgnoreMember] public const string StartedFail = "StartedFail";
        [IgnoreMember] public const int ErrorCode = 12001;

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public TradeOrderEntityId EntityId { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }

        [Key(8)] public TradeType TradeType { get; init; }
        [Key(9)] public string? TradeStrategyName { get; init; }

        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public string EventName => GetType().Name;
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public TradeStrategyStartedEvent() { }

        /// <summary>
        /// MessagePack constructor used for deserialization.
        /// </summary>
        [SerializationConstructor]
        public TradeStrategyStartedEvent(ActorSubject subject, Guid id, TradeOrderEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, TradeType tradeType, string? tradeStrategyName)
        {
            Subject = subject;
            EntityId = entityId;
            Id = id;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradeType = tradeType;
            TradeStrategyName = tradeStrategyName;
        }

        /// <summary>
        /// Convert this denormalize event into a completed event which indicates successful handling.
        /// </summary>
        public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
            where TComplete : ICompleteEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            if (typeof(TEntityId) != typeof(TradeOrderEntityId))
                throw new InvalidOperationException($"{EventName}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(TradeOrderEntityId).FullName}.");

            var completed = new TradeStrategyStartedCompleteEvent
            {
                Subject = new ActorSubject(ActorType.Event, Actor, StartedComplete, this.Subject.EntityId),
                EntityId = this.EntityId,
                Id = this.Id,
                EventId = this.EventId,
                CommandId = this.CommandId,
                AggregateId = this.AggregateId,
                EventSource = this.EventSource,
                ReceivedOn = this.ReceivedOn,
                TradeType = this.TradeType,
                TradeStrategyName = this.TradeStrategyName
            };

            return (ICompleteEvent<TEntityId>)(object)completed;
        }

        /// <summary>
        /// Convert this denormalize event into a failed error event describing the provided exception.
        /// </summary>
        /// <param name="ex">The exception encountered while denormalizing.</param>
        public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
            where TFail : IErrorEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            var failed = new TradeStrategyStartedFailEvent
            {
                Subject = new ActorSubject(ActorType.Event, Actor, StartedFail, this.Subject.EntityId),
                EntityId = this.EntityId,
                Id = this.Id,
                EventId = this.EventId,
                CommandId = this.CommandId,
                AggregateId = this.AggregateId,
                EventSource = this.EventSource,
                ReceivedOn = this.ReceivedOn,
                ErrorMessage = ex.Message,
                ErrorType = ErrorType.Command,
                ErrorCode = ErrorCode
            };
            return (IErrorEvent<TEntityId>)(object)failed;
        }
    }

    /// <summary>
    /// Event published when a trade strategy start operation has been completed successfully.
    /// Carries metadata from the original event.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    public record TradeStrategyStartedCompleteEvent : ICompleteEvent<TradeOrderEntityId>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public TradeOrderEntityId EntityId { get; init; }
        [Key(2)] public Guid Id { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }
        [Key(8)] public TradeType TradeType { get; init; }
        [Key(9)] public string? TradeStrategyName { get; init; }

        [IgnoreMember]
        public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";

        [IgnoreMember]
        public string EventName => GetType().Name;

        [IgnoreMember]
        public EventType EventType => EventType.CompletedEvent;

        public TradeStrategyStartedCompleteEvent() { }

        [SerializationConstructor]
        public TradeStrategyStartedCompleteEvent(ActorSubject subject, TradeOrderEntityId entityId, Guid id, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, TradeType tradeType, string? tradeStrategyName)
        {
            Subject = subject;
            EntityId = entityId;
            Id = id;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            TradeType = tradeType;
            TradeStrategyName = tradeStrategyName;
        }
    }

    /// <summary>
    /// Event published when a trade strategy start operation has failed.
    /// Inherits standardized error details from <see cref="ErrorEvent"/>.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    public record TradeStrategyStartedFailEvent : IErrorEvent<TradeOrderEntityId>
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public TradeOrderEntityId EntityId { get; init; }
        [Key(2)] public Guid Id { get; init; }
        [Key(3)] public DateTime ErrorDate { get; init; }
        [Key(4)] public long EventId { get; init; }
        [Key(5)] public Guid CommandId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public string ErrorMessage { get; init; }
        [Key(8)] public int ErrorCode { get; init; }
        [Key(9)] public ErrorType ErrorType { get; init; }
        [Key(10)] public string ErrorData { get; init; }
        [Key(11)] public DateTime ReceivedOn { get; init; }
        [Key(12)] public string AggregateId { get; init; }
        [Key(13)] public string CommandName { get; init; }
        [Key(14)] public string CommandData { get; init; }
        [Key(15)] public string RouteTo { get; init; }

        [IgnoreMember] public string EventName => this.GetType().Name;
        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

        public TradeStrategyStartedFailEvent() { }

        [SerializationConstructor]
        public TradeStrategyStartedFailEvent(ActorSubject subject, TradeOrderEntityId entityId, Guid id, DateTime errorDate, long eventId, Guid commandId, string eventSource, string errorMessage, int errorCode, ErrorType errorType, string errorData, DateTime receivedOn, string aggregateId, string commandName, string commandData, string routeTo)
        {
            Subject = subject;
            EntityId = entityId;
            Id = id;
            ErrorDate = errorDate;
            EventId = eventId;
            CommandId = commandId;
            EventSource = eventSource ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            ErrorCode = errorCode;
            ErrorType = errorType;
            ErrorData = errorData ?? string.Empty;
            ReceivedOn = receivedOn;
            AggregateId = aggregateId ?? string.Empty;
            CommandName = commandName ?? string.Empty;
            CommandData = commandData ?? string.Empty;
            RouteTo = routeTo ?? string.Empty;
        }
    }
}
