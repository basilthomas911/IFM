using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events;

    [MessagePackObject(AllowPrivate = true)]
    public record OptionTradePositionOpenedEvent : IEvent<OptionTradeEntityId>
    {
        [IgnoreMember] public const string Actor = "OptionTradeEvent";
        [IgnoreMember] public const string Verb = "PositionOpened";
        [IgnoreMember] public const int ErrorCode = 7031;

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public OptionTradeEntityId EntityId { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }

        // payload (keys 8..)
        [Key(8)] public OptionTradeEntityId OptionTradeId { get; init; }
        [Key(9)] public TradePositionState TradePositionState { get; init; }
        [Key(10)] public DateTime OpenedOn { get; init; }
        [Key(11)] public string OpenedBy { get; init; }

        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public string EventName => GetType().Name;
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;

        public OptionTradePositionOpenedEvent() { }

        [SerializationConstructor]
        public OptionTradePositionOpenedEvent(
            ActorSubject subject,
            Guid id,
            OptionTradeEntityId entityId,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            OptionTradeEntityId optionTradeId,
            TradePositionState tradePositionState,
            DateTime openedOn,
            string openedBy)
        {
            Subject = subject;
            Id = id;
            EntityId = entityId;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            OptionTradeId = optionTradeId;
            TradePositionState = tradePositionState;
            OpenedOn = openedOn;
            OpenedBy = openedBy ?? string.Empty;
        }

        public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
            where TComplete : ICompleteEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            if (typeof(TEntityId) != typeof(OptionTradeEntityId))
                throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

            ICompleteEvent<OptionTradeEntityId> completed = new OptionTradePositionOpenedCompleteEvent
            {
                Subject = new ActorSubject(ActorType.Event, OptionTradePositionOpenedCompleteEvent.Actor, OptionTradePositionOpenedCompleteEvent.Verb, this.Subject.EntityId),
                EntityId = this.EntityId,
                Id = this.Id,
                EventId = this.EventId,
                CommandId = this.CommandId,
                AggregateId = this.AggregateId,
                EventSource = this.EventSource,
                ReceivedOn = this.ReceivedOn,
                OptionTradeId = this.OptionTradeId,
                TradePositionState = this.TradePositionState,
                OpenedOn = this.OpenedOn,
                OpenedBy = this.OpenedBy
            };

            return (ICompleteEvent<TEntityId>)completed;
        }

        public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
            where TFail : IErrorEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            if (typeof(TEntityId) != typeof(OptionTradeEntityId))
                throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(OptionTradeEntityId).FullName}.");

            IErrorEvent<OptionTradeEntityId> failed = new OptionTradePositionOpenedFailEvent
            {
                Subject = new ActorSubject(ActorType.Event, OptionTradePositionOpenedFailEvent.Actor, OptionTradePositionOpenedFailEvent.Verb, this.Subject.EntityId),
                EntityId = this.EntityId,
                Id = this.Id,
                ErrorDate = DateTime.UtcNow,
                EventId = this.EventId,
                CommandId = this.CommandId == Guid.Empty ? Guid.NewGuid() : this.CommandId,
                EventSource = this.EventSource,
                ErrorMessage = ex.Message,
                ErrorType = ErrorType.Command,
                ErrorCode = ErrorCode,
                ErrorData = ex.ToString(),
                ReceivedOn = this.ReceivedOn,
                AggregateId = this.AggregateId
            };

            return (IErrorEvent<TEntityId>)failed;
        }
    }

    [MessagePackObject(AllowPrivate = true)]
    public record OptionTradePositionOpenedCompleteEvent : ICompleteEvent<OptionTradeEntityId>
    {
        [IgnoreMember] public const string Actor = "OptionTradeEvent";
        [IgnoreMember] public const string Verb = "PositionOpenedComplete";

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public OptionTradeEntityId EntityId { get; init; }
        [Key(2)] public Guid Id { get; init; }
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; }
        [Key(6)] public string EventSource { get; init; }
        [Key(7)] public DateTime ReceivedOn { get; init; }

        [Key(8)] public OptionTradeEntityId OptionTradeId { get; init; }
        [Key(9)] public TradePositionState TradePositionState { get; init; }
        [Key(10)] public DateTime OpenedOn { get; init; }
        [Key(11)] public string OpenedBy { get; init; }

        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public string EventName => GetType().Name;
        [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

        public OptionTradePositionOpenedCompleteEvent() { }

        [SerializationConstructor]
        public OptionTradePositionOpenedCompleteEvent(
            ActorSubject subject,
            OptionTradeEntityId entityId,
            Guid id,
            long eventId,
            Guid commandId,
            string aggregateId,
            string eventSource,
            DateTime receivedOn,
            OptionTradeEntityId optionTradeId,
            TradePositionState tradePositionState,
            DateTime openedOn,
            string openedBy)
        {
            Subject = subject;
            EntityId = entityId;
            Id = id;
            EventId = eventId;
            CommandId = commandId;
            AggregateId = aggregateId ?? string.Empty;
            EventSource = eventSource ?? string.Empty;
            ReceivedOn = receivedOn;
            OptionTradeId = optionTradeId;
            TradePositionState = tradePositionState;
            OpenedOn = openedOn;
            OpenedBy = openedBy ?? string.Empty;
        }
    }

    [MessagePackObject(AllowPrivate = true)]
    public record OptionTradePositionOpenedFailEvent : IErrorEvent<OptionTradeEntityId>
    {
        [IgnoreMember] public const string Actor = "OptionTradeEvent";
        [IgnoreMember] public const string Verb = "PositionOpenedFail";

        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public OptionTradeEntityId EntityId { get; init; }
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

        [IgnoreMember] public string EventName => GetType().Name;
        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

        public OptionTradePositionOpenedFailEvent() { }

        [SerializationConstructor]
        public OptionTradePositionOpenedFailEvent(
            ActorSubject subject,
            OptionTradeEntityId entityId,
            Guid id,
            DateTime errorDate,
            long eventId,
            Guid commandId,
            string eventSource,
            string errorMessage,
            int errorCode,
            ErrorType errorType,
            string errorData,
            DateTime receivedOn,
            string aggregateId,
            string commandName,
            string commandData,
            string routeTo)
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
