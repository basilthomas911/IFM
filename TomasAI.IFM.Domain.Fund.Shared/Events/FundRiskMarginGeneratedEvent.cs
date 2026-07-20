using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Events;

/// <summary>
/// Represents a domain event indicating that the maximum profit for a fund has been generated. This event contains
/// metadata and payload information related to the fund order and the calculated maximum profit.
/// </summary>
/// <remarks>This event is typically used within event-driven architectures to signal the successful generation of
/// a fund's maximum profit. It provides both identifying metadata and detailed payloads for downstream processing or
/// auditing. The event supports conversion to completion or failure events for workflow tracking. Thread safety is not
/// guaranteed; synchronize access if used concurrently.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundMaxProfitGeneratedEvent : IEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "MaxProfitGenerated";
    [IgnoreMember] public const int ErrorCode = 3024;

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
    [Key(8)] public FundOrderReadModel FundOrder { get; init; }
    [Key(9)] public FundMaxProfitReadModel FundMaxProfit { get; set; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FundMaxProfitGeneratedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FundMaxProfitGeneratedEvent(
        ActorSubject subject,
        Guid id,
        FundId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderReadModel fundOrder,
        FundMaxProfitReadModel fundMaxProfit)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        FundMaxProfit = fundMaxProfit;
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

        ICompleteEvent<FundId> completed = new FundMaxProfitGeneratedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundMaxProfitGeneratedCompleteEvent.Actor, FundMaxProfitGeneratedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FundOrder = this.FundOrder,
            FundMaxProfit = this.FundMaxProfit
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

        IErrorEvent<FundId> failed = new FundMaxProfitGeneratedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FundMaxProfitGeneratedFailEvent.Actor, FundMaxProfitGeneratedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a fund max-profit generation operation has completed successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundMaxProfitGeneratedCompleteEvent : ICompleteEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "MaxProfitGeneratedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FundId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FundOrderReadModel FundOrder { get; init; }
    [Key(9)] public FundMaxProfitReadModel FundMaxProfit { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FundMaxProfitGeneratedCompleteEvent() { }

    [SerializationConstructor]
    public FundMaxProfitGeneratedCompleteEvent(
        ActorSubject subject,
        FundId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FundOrderReadModel fundOrder,
        FundMaxProfitReadModel fundMaxProfit)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        FundMaxProfit = fundMaxProfit ?? throw new ArgumentNullException(nameof(fundMaxProfit));
    }
}

/// <summary>
/// Event published when a fund max-profit generation operation has failed.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FundMaxProfitGeneratedFailEvent : IErrorEvent<FundId>
{
    [IgnoreMember] public const string Actor = "FundEvent";
    [IgnoreMember] public const string Verb = "MaxProfitGeneratedFail";

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

    public FundMaxProfitGeneratedFailEvent() { }

    [SerializationConstructor]
    public FundMaxProfitGeneratedFailEvent(
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