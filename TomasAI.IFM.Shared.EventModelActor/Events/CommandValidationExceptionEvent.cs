using System;
using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Events
{
    /// <summary>
    /// Exception-level error event produced when a command validation failure occurs.
    /// Targets a generic actor entity id (<see cref="ActorEntityId"/>). The <see cref="EntityId"/>
    /// defaults to <see cref="ActorEntityId.Default"/>.
    /// </summary>
    [MessagePackObject(AllowPrivate = true)]
    public class CommandValidationExceptionEvent : IErrorEvent<ActorEntityId>, IExceptionEvent
    {
        [Key(0)] public ActorSubject Subject { get; set; } = default!;
        [Key(1)] public ActorEntityId EntityId { get; set; } = ActorEntityId.Default;
        [Key(2)] public Guid Id { get; set; } = Guid.NewGuid();
        [Key(3)] public DateTime ErrorDate { get; set; } = DateTime.UtcNow;
        [Key(4)] public long EventId { get; set; }
        [Key(5)] public Guid CommandId { get; set; }
        [Key(6)] public string EventSource { get; set; } = string.Empty;
        [Key(7)] public string ErrorMessage { get; set; } = string.Empty;
        [Key(8)] public int ErrorCode { get; set; }
        [Key(9)] public ErrorType ErrorType { get; set; }
        [Key(10)] public string ErrorData { get; set; } = string.Empty;
        [Key(11)] public DateTime ReceivedOn { get; set; }
        [Key(12)] public string AggregateId { get; set; } = string.Empty;
        [Key(13)] public string CommandName { get; set; } = string.Empty;
        [Key(14)] public string CommandData { get; set; } = string.Empty;
        [Key(15)] public string RouteTo { get; set; } = string.Empty;

        [IgnoreMember] public string EventName => GetType().Name;
        [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
        [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

        /// <summary>
        /// Default constructor - ensures <see cref="EntityId"/> defaults to <see cref="ActorEntityId.Default"/>.
        /// </summary>
        public CommandValidationExceptionEvent() { EntityId = ActorEntityId.Default; }

        /// <summary>
        /// MessagePack constructor used for deserialization.
        /// </summary>
        [SerializationConstructor]
        public CommandValidationExceptionEvent(
            ActorSubject subject,
            ActorEntityId entityId,
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
            EntityId = entityId ?? ActorEntityId.Default;
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
