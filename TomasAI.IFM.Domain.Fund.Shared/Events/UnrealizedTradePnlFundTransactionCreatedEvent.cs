using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Event emitted when an unrealized-trade PnL fund transaction is created.
/// and update read models. Targets <see cref="FundTransactionEntityId"/> as the actor entity id
/// (matches create-fund-transaction commands that produce transaction entity ids).
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record UnrealizedTradePnlFundTransactionCreatedEvent : IEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransaction";
    [IgnoreMember] public const string Verb = "UnrealizedTradePnlFundTransactionCreated";
    [IgnoreMember] public const int ErrorCode = 3012;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundTransactionEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FundTransactionReadModel FundTransaction { get; init; }
    [Key(9)] public string CreatedBy { get; init; }
    [Key(10)] public DateTime CreatedOn { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public UnrealizedTradePnlFundTransactionCreatedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public UnrealizedTradePnlFundTransactionCreatedEvent(
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
        FundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundTransactionEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundTransactionEntityId).FullName}.");

        ICompleteEvent<FundTransactionEntityId> completed = new UnrealizedTradePnlFundTransactionCreatedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, UnrealizedTradePnlFundTransactionCreatedCompleteEvent.Actor, UnrealizedTradePnlFundTransactionCreatedCompleteEvent.Verb, this.Subject.EntityId),
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

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// Validates the requested entity id type and returns a strongly-typed error event.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundTransactionEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundTransactionEntityId).FullName}.");

        IErrorEvent<FundTransactionEntityId> failed = new UnrealizedTradePnlFundTransactionCreatedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, UnrealizedTradePnlFundTransactionCreatedFailEvent.Actor, UnrealizedTradePnlFundTransactionCreatedFailEvent.Verb, this.Subject.EntityId),
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

/// <summary>
/// Event published when an unrealized-trade PnL fund transaction has been created successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record UnrealizedTradePnlFundTransactionCreatedCompleteEvent : ICompleteEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransaction";
    [IgnoreMember] public const string Verb = "UnrealizedTradePnlFundTransactionCreatedComplete";
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

    public UnrealizedTradePnlFundTransactionCreatedCompleteEvent() { }

    [SerializationConstructor]
    public UnrealizedTradePnlFundTransactionCreatedCompleteEvent(
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
        FundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        CreatedBy = createdBy ?? string.Empty;
        CreatedOn = createdOn;
    }
}

/// <summary>
/// Event published when creating an unrealized-trade PnL fund transaction has failed.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record UnrealizedTradePnlFundTransactionCreatedFailEvent : IErrorEvent<FundTransactionEntityId>
{
    [IgnoreMember] public const string Actor = "FundTransaction";
    [IgnoreMember] public const string Verb = "UnrealizedTradePnlFundTransactionCreatedFail";
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

    public UnrealizedTradePnlFundTransactionCreatedFailEvent() { }

    [SerializationConstructor]
    public UnrealizedTradePnlFundTransactionCreatedFailEvent(
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
