using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Represents a domain event indicating that the trade state of a fund order has changed.
/// </summary>
/// <remarks>This event is typically published when the trade state of a fund order transitions, such as from
/// pending to completed or failed. It contains metadata and payload information relevant to the state change, and can
/// be used in event-driven architectures to trigger downstream processing or auditing. The event supports conversion to
/// corresponding completion or failure events for further handling.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeStateChangedEvent : IEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderTradeStateChanged";
    [IgnoreMember] public const string CompleteEventName = "FundOrderTradeStateChangedComplete";
    [IgnoreMember] public const string Fail = "FundOrderTradeStateChangedFail";
    [IgnoreMember] public const int ErrorCode = 3007;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FundId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FundOrderTradeId FundOrderTradeId { get; init; }
    [Key(9)] public TradeState TradeState { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FundOrderTradeStateChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FundOrderTradeStateChangedEvent(
        ActorSubject subject,
        Guid id,
        FundId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderTradeId fundOrderTradeId,
        TradeState tradeState,
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
        FundOrderTradeId = fundOrderTradeId;    
        TradeState = tradeState;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        ICompleteEvent<FundId> completed = new FundOrderTradeStateChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundOrderTradeStateChangedCompleteEvent.Actor, FundOrderTradeStateChangedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundOrderTradeId = this.FundOrderTradeId,
            TradeState = this.TradeState,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
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
        if (typeof(TEntityId) != typeof(FundId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FundId).FullName}.");

        IErrorEvent<FundId> failed = new FundOrderTradeStateChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundOrderTradeStateChangedFailEvent.Actor, FundOrderTradeStateChangedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a fund order trade state change has completed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeStateChangedCompleteEvent : ICompleteEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderTradeStateChangedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FundOrderTradeId FundOrderTradeId { get; init; }
    [Key(9)] public TradeState TradeState { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FundOrderTradeStateChangedCompleteEvent() { }

    [SerializationConstructor]
    public FundOrderTradeStateChangedCompleteEvent(
        ActorSubject subject,
        FundId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderTradeId fundOrderTradeId,
        TradeState tradeState,
        DateTime updatedOn,
        string updatedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrderTradeId = fundOrderTradeId;
        TradeState = tradeState;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when a fund order trade state change has failed.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeStateChangedFailEvent : IErrorEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "FundOrderTradeStateChangedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
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

    public FundOrderTradeStateChangedFailEvent() { }

    [SerializationConstructor]
    public FundOrderTradeStateChangedFailEvent(
        ActorSubject subject,
        FundId entityId,
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