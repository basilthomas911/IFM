using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Emitted when a fund transaction that affects the closing balance is created.
/// and update read models. Targets <see cref="FundTransactionEntityId"/> as the actor entity id.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundBalanceChangedEvent : IEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransaction";
    [IgnoreMember] public const string Verb = "BalanceChanged";
    [IgnoreMember] public const string BalanceChangedComplete = "BalanceChangedComplete";
    [IgnoreMember] public const string BalanceChangedFail = "BalanceChangedFail";
    [IgnoreMember] public const int ErrorCode = 3025;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundTransactionEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload
    [Key(8)] public FundTransactionReadModel FundTransaction { get; init; }
    [Key(9)] public DateTime CreatedOn { get; init; }
    [Key(10)] public string CreatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FundBalanceChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// Keys must match the <see cref="KeyAttribute"/> indices.
    /// </summary>
    [SerializationConstructor]
    public FundBalanceChangedEvent(
        ActorSubject subject,
        Guid id,
        FundTransactionEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundTransactionReadModel fundTransaction,
        DateTime createdOn,
        string createdBy)
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
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Convert into a completed event after successful denormalization.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundTransactionEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompletedEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundTransactionEntityId).FullName}.");

        var completed = new FundBalanceChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, BalanceChangedComplete, this.Subject.EntityId),
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
    /// Convert into an error event describing the failure encountered while denormalizing.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var failed = new FundBalanceChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, Actor, BalanceChangedFail, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            ErrorDate = DateTime.UtcNow,
            EventId = this.EventId,
            CommandId = this.CommandId,
            EventSource = this.EventSource,
            ErrorMessage = ex.Message,
            ErrorType = ErrorType.Denormalizer,
            ErrorCode = ErrorCode,
            ErrorData = ex.ToString(),
            ReceivedOn = this.ReceivedOn,
            AggregateId = this.AggregateId
        };

        return (IErrorEvent<TEntityId>)(object)failed;
    }
}

/// <summary>
/// Completed event published after a fund balance change has been successfully denormalized.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundBalanceChangedCompleteEvent : ICompleteEvent<FundTransactionEntityId>
{
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

    public FundBalanceChangedCompleteEvent() { }

    [SerializationConstructor]
    public FundBalanceChangedCompleteEvent(
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
/// Error event published when denormalizing a fund balance change fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundBalanceChangedFailEvent : IErrorEvent<FundTransactionEntityId>
{
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

    public FundBalanceChangedFailEvent() { }

    [SerializationConstructor]
    public FundBalanceChangedFailEvent(
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
