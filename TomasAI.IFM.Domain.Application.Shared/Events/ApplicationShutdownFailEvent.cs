using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Events;

/// <summary>
/// Error event published when handling the application shutdown failed.
/// Contains standardized error details.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record ApplicationShutdownFailEvent : IErrorEvent<ApplicationEntityId>
{
    [IgnoreMember] public const string Actor = "ApplicationEvent";
    [IgnoreMember] public const string Verb = "ShutdownFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ApplicationEntityId EntityId { get; init; }
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

    /// <summary>Creates an empty shutdown fail event for serialization.</summary>
    public ApplicationShutdownFailEvent() { }

    /// <summary>Rehydrates the application failure notification from its wire fields.</summary>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="id">The event identifier.</param>
    /// <param name="errorDate">The failure timestamp.</param>
    /// <param name="eventId">The durable event sequence identifier.</param>
    /// <param name="commandId">The originating command identifier.</param>
    /// <param name="eventSource">The event source.</param>
    /// <param name="errorMessage">The failure description.</param>
    /// <param name="errorCode">The stable failure code.</param>
    /// <param name="errorType">The failure classification.</param>
    /// <param name="errorData">The failure details.</param>
    /// <param name="receivedOn">The receive timestamp.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="commandName">The originating command name.</param>
    /// <param name="commandData">The originating command data.</param>
    /// <param name="routeTo">The reply route.</param>
    [SerializationConstructor]
    public ApplicationShutdownFailEvent(
        ActorSubject subject,
        ApplicationEntityId entityId,
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
