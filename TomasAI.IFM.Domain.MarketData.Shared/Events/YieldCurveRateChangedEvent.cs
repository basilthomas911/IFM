using System;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.Events;

/// <summary>
/// Represents a domain event indicating that a yield curve rate entry has been changed. Used to communicate the modification
/// of a yield curve rate entry within the event-driven system and to support denormalization workflows.
/// </summary>
/// <remarks>This event is typically published when a yield curve rate entry is successfully updated. It carries
/// metadata and the details of the changed yield curve rate entry for downstream processing, auditing, or projection
/// updates. The event supports conversion to completion or failure events to indicate the outcome of related command
/// handling. Thread safety is not guaranteed; instances are generally used as data transfer objects within event
/// processing pipelines.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateChangedEvent : IEvent<YieldCurveRateEntityId>
{
    [IgnoreMember] public const string Actor = "YieldCurveRateEvent";
    [IgnoreMember] public const string Verb = "Changed";
    [IgnoreMember] public const int ErrorCode = 2008;

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
    [Key(8)] public YieldCurveRateReadModel YieldCurveRate { get; init; }
    [Key(9)] public DateTime UpdatedOn { get; init; }
    [Key(10)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public YieldCurveRateChangedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public YieldCurveRateChangedEvent(
        ActorSubject subject,
        Guid id,
        YieldCurveRateEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        YieldCurveRateReadModel yieldCurveRate,
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
        YieldCurveRate = yieldCurveRate ?? throw new ArgumentNullException(nameof(yieldCurveRate));
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
        if (typeof(TEntityId) != typeof(YieldCurveRateEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(YieldCurveRateEntityId).FullName}.");

        ICompleteEvent<YieldCurveRateEntityId> completed = new YieldCurveRateChangedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRateChangedCompleteEvent.Actor, YieldCurveRateChangedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            YieldCurveRate = this.YieldCurveRate,
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
        ArgumentNullException.ThrowIfNull(ex);
        if (typeof(TEntityId) != typeof(YieldCurveRateEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(YieldCurveRateEntityId).FullName}.");

        IErrorEvent<YieldCurveRateEntityId> failed = new YieldCurveRateChangedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, YieldCurveRateChangedFailEvent.Actor, YieldCurveRateChangedFailEvent.Verb, this.Subject.EntityId),
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
