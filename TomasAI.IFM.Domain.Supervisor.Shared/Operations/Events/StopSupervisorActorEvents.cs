using MessagePack;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

/// <summary>Reports successful Stop Supervisor operation projection.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record StopSupervisorActorCompleteEvent : ICompleteEvent<ActorEntityId>
{
    public const string Actor = "SupervisorEvent";
    public const string Verb = "StopComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = "SupervisorCommandActor";
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ActorThreadId Target { get; init; }
    [Key(9)] public long ExpectedGeneration { get; init; }
    [Key(10)] public string Requester { get; init; } = string.Empty;
    [Key(11)] public string Reason { get; init; } = string.Empty;
    [Key(12)] public string Stage { get; init; } = string.Empty;
    [Key(13)] public long TimeoutTicks { get; init; }
    [IgnoreMember] public string EventName => nameof(StopSupervisorActorCompleteEvent);
    [IgnoreMember] public string UserName => Requester;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    /// <summary>Creates the event for serialization and object-initializer callers.</summary>
    public StopSupervisorActorCompleteEvent() { }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name="subject">The serialized Subject value.</param>
    /// <param name="entityId">The serialized EntityId value.</param>
    /// <param name="id">The serialized Id value.</param>
    /// <param name="eventId">The serialized EventId value.</param>
    /// <param name="commandId">The serialized CommandId value.</param>
    /// <param name="aggregateId">The serialized AggregateId value.</param>
    /// <param name="eventSource">The serialized EventSource value.</param>
    /// <param name="receivedOn">The serialized ReceivedOn value.</param>
    /// <param name="target">The serialized Target value.</param>
    /// <param name="expectedGeneration">The serialized ExpectedGeneration value.</param>
    /// <param name="requester">The serialized Requester value.</param>
    /// <param name="reason">The serialized Reason value.</param>
    /// <param name="stage">The serialized Stage value.</param>
    /// <param name="timeoutTicks">The serialized TimeoutTicks value.</param>
    [SerializationConstructor]
    public StopSupervisorActorCompleteEvent(ActorSubject subject,
        ActorEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        ActorThreadId target,
        long expectedGeneration,
        string requester,
        string reason,
        string stage,
        long timeoutTicks)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Requester = requester;
        Reason = reason;
        Stage = stage;
        TimeoutTicks = timeoutTicks;
    }
}

/// <summary>Reports a known failure of the Stop Supervisor operation.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record StopSupervisorActorFailEvent : IErrorEvent<ActorEntityId>
{
    public const string Actor = "SupervisorEvent";
    public const string Verb = "StopFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; } = "SupervisorCommandActor";
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(8)] public int ErrorCode { get; init; } = 9714;
    [Key(9)] public ErrorType ErrorType { get; init; } = ErrorType.Command;
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = "StopSupervisorActorCommand";
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    [Key(15)] public string RouteTo { get; init; } = string.Empty;
    [Key(16)] public ActorThreadId Target { get; init; }
    [Key(17)] public long ExpectedGeneration { get; init; }
    [Key(18)] public string Requester { get; init; } = string.Empty;
    [Key(19)] public string Reason { get; init; } = string.Empty;
    [Key(20)] public string Stage { get; init; } = string.Empty;
    [Key(21)] public long TimeoutTicks { get; init; }
    [Key(22)] public SupervisorOperationOutcome Outcome { get; init; }
    [IgnoreMember] public string EventName => nameof(StopSupervisorActorFailEvent);
    [IgnoreMember] public string UserName => Requester;
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;

    /// <summary>Creates the event for serialization and object-initializer callers.</summary>
    public StopSupervisorActorFailEvent() { }

    /// <summary>Reconstructs every permanent MessagePack field in numeric key order.</summary>
    /// <param name="subject">The serialized Subject value.</param>
    /// <param name="entityId">The serialized EntityId value.</param>
    /// <param name="id">The serialized Id value.</param>
    /// <param name="errorDate">The serialized ErrorDate value.</param>
    /// <param name="eventId">The serialized EventId value.</param>
    /// <param name="commandId">The serialized CommandId value.</param>
    /// <param name="eventSource">The serialized EventSource value.</param>
    /// <param name="errorMessage">The serialized ErrorMessage value.</param>
    /// <param name="errorCode">The serialized ErrorCode value.</param>
    /// <param name="errorType">The serialized ErrorType value.</param>
    /// <param name="errorData">The serialized ErrorData value.</param>
    /// <param name="receivedOn">The serialized ReceivedOn value.</param>
    /// <param name="aggregateId">The serialized AggregateId value.</param>
    /// <param name="commandName">The serialized CommandName value.</param>
    /// <param name="commandData">The serialized CommandData value.</param>
    /// <param name="routeTo">The serialized RouteTo value.</param>
    /// <param name="target">The serialized Target value.</param>
    /// <param name="expectedGeneration">The serialized ExpectedGeneration value.</param>
    /// <param name="requester">The serialized Requester value.</param>
    /// <param name="reason">The serialized Reason value.</param>
    /// <param name="stage">The serialized Stage value.</param>
    /// <param name="timeoutTicks">The serialized TimeoutTicks value.</param>
    /// <param name="outcome">The serialized Outcome value.</param>
    [SerializationConstructor]
    public StopSupervisorActorFailEvent(ActorSubject subject,
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
        string routeTo,
        ActorThreadId target,
        long expectedGeneration,
        string requester,
        string reason,
        string stage,
        long timeoutTicks,
        SupervisorOperationOutcome outcome)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        ErrorDate = errorDate;
        EventId = eventId;
        CommandId = commandId;
        EventSource = eventSource;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        ErrorType = errorType;
        ErrorData = errorData;
        ReceivedOn = receivedOn;
        AggregateId = aggregateId;
        CommandName = commandName;
        CommandData = commandData;
        RouteTo = routeTo;
        Target = target;
        ExpectedGeneration = expectedGeneration;
        Requester = requester;
        Reason = reason;
        Stage = stage;
        TimeoutTicks = timeoutTicks;
        Outcome = outcome;
    }
}
