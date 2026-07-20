using System;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Events;

/// <summary>
/// Represents a domain event indicating that yield curve rates have been imported. Used to communicate the import
/// of yield curve rates within the event-driven system and to support denormalization workflows.
/// </summary>
/// <remarks>This event is typically published when yield curve rates are successfully imported. It carries
/// metadata and the details of the imported yield curve rates for downstream processing, auditing, or projection
/// updates. The event supports conversion to completion or failure events to indicate the outcome of related command
/// handling. Thread safety is not guaranteed; instances are generally used as data transfer objects within event
/// processing pipelines.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRatesImportedEvent : IEvent<YieldCurveRateEntityId>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "Imported";
    [IgnoreMember] public const int ErrorCode = 2010;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public DateTime ImportDate { get; init; }
    [Key(9)] public YieldCurveRateReadModel[] YieldCurveRates { get; init; }
    [Key(10)] public DateTime ImportedOn { get; init; }
    [Key(11)] public string ImportedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public YieldCurveRatesImportedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public YieldCurveRatesImportedEvent(
        ActorSubject subject,
        Guid id,
        YieldCurveRateEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime importDate,
        YieldCurveRateReadModel[] yieldCurveRates,
        DateTime importedOn,
        string importedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ImportDate = importDate;
        YieldCurveRates = yieldCurveRates ?? [];
        ImportedOn = importedOn;
        ImportedBy = importedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// Validates the requested entity id type and returns a strongly-typed complete event.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(YieldCurveRateEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(YieldCurveRateEntityId).FullName}.");

        ICompleteEvent<YieldCurveRateEntityId> completed = new YieldCurveRatesImportedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedCompleteEvent.Actor, YieldCurveRatesImportedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            ImportDate = this.ImportDate,
            YieldCurveRates = this.YieldCurveRates,
            ImportedOn = this.ImportedOn,
            ImportedBy = this.ImportedBy
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
        if (typeof(TEntityId) != typeof(YieldCurveRateEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(YieldCurveRateEntityId).FullName}.");

        IErrorEvent<YieldCurveRateEntityId> failed = new YieldCurveRatesImportedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedFailEvent.Actor, YieldCurveRatesImportedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when yield curve rates have been imported successfully.
/// Carries metadata from the original event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRatesImportedCompleteEvent : ICompleteEvent<YieldCurveRateEntityId>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "ImportedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public DateTime ImportDate { get; init; }
    [Key(9)] public YieldCurveRateReadModel[] YieldCurveRates { get; init; }
    [Key(10)] public DateTime ImportedOn { get; init; }
    [Key(11)] public string ImportedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public YieldCurveRatesImportedCompleteEvent() { }

    [SerializationConstructor]
    public YieldCurveRatesImportedCompleteEvent(
        ActorSubject subject,
        YieldCurveRateEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        DateTime importDate,
        YieldCurveRateReadModel[] yieldCurveRates,
        DateTime importedOn,
        string importedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        ImportDate = importDate;
        YieldCurveRates = yieldCurveRates ?? [];
        ImportedOn = importedOn;
        ImportedBy = importedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when importing yield curve rates fails.
/// Carries standardized error details from the error event contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRatesImportedFailEvent : IErrorEvent<YieldCurveRateEntityId>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "ImportedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public YieldCurveRateEntityId EntityId { get; init; }
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

    public YieldCurveRatesImportedFailEvent() { }

    [SerializationConstructor]
    public YieldCurveRatesImportedFailEvent(
        ActorSubject subject,
        YieldCurveRateEntityId entityId,
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
