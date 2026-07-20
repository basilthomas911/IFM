using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Event emitted when an end-of-day fund transaction has been processed.
/// and update read models. Targets <see cref="FundTransactionEntityId"/> as the actor entity id.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EndOfDayFundTransactionProcessedEvent : IEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const int ErrorCode = 3019;
    [IgnoreMember] public const string Actor = "FundTransactionEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessed";
    [IgnoreMember] public const string ProcessedComplete = "EndOfDayProcessedComplete";
    [IgnoreMember] public const string ProcessedFail = "EndOfDayProcessedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundTransactionEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // Payload keys (8..)
    [Key(8)] public FundTransactionReadModel FundTransaction { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public EndOfDayFundTransactionProcessedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public EndOfDayFundTransactionProcessedEvent(
        ActorSubject subject,
        Guid id,
        FundTransactionEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundTransactionReadModel fundTransaction,
        string createdBy,
        DateTime createdOn)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundTransaction = fundTransaction;
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundTransactionEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundTransactionEntityId).FullName}.");

        var completed = new EndOfDayFundTransactionProcessedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, ProcessedComplete, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundTransaction = this.FundTransaction,
            CreatedBy = this.CreatedBy,
            CreatedOn = this.CreatedOn
        };

        return (ICompleteEvent<TEntityId>)(object)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new EndOfDayFundTransactionProcessedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, ProcessedFail, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)(object)failed;
    }
}

/// <summary>
/// Event published when an end-of-day fund transaction processing has completed successfully.
/// Carries the processed transaction and metadata.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EndOfDayFundTransactionProcessedCompleteEvent : ICompleteEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransactionEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessedComplete";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundTransactionEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FundTransactionReadModel FundTransaction { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public EndOfDayFundTransactionProcessedCompleteEvent() { }

    [SerializationConstructor]
    public EndOfDayFundTransactionProcessedCompleteEvent(
        ActorSubject subject,
        FundTransactionEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundTransactionReadModel fundTransaction,
        string createdBy,
        DateTime createdOn)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundTransaction = fundTransaction;
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }
}

/// <summary>
/// Event published when an end-of-day fund transaction processing has failed.
/// Provides standardized error details for denormalization failures.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record EndOfDayFundTransactionProcessedFailEvent : IErrorEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransactionEvent";
    [IgnoreMember] public const string Verb = "EndOfDayProcessedFail";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundTransactionEntityId EntityId { get; init; }
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

    public EndOfDayFundTransactionProcessedFailEvent() { }

    [SerializationConstructor]
    public EndOfDayFundTransactionProcessedFailEvent(
        ActorSubject subject,
        FundTransactionEntityId entityId,
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
