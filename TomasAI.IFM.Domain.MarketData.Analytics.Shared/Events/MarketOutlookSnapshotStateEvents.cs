using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

/// <summary>Records an immutable Market Outlook working-state transition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookComponentObservedEvent : IEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = "MarketOutlookSnapshotEvent";
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "ComponentObserved";
    /// <summary>Gets the projection error code.</summary>
    [IgnoreMember] public const int ErrorCode = 19101;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }

    /// <summary>Gets the complete working state after the component was incorporated.</summary>
    [Key(8)] public MarketOutlookWorkingStateReadModel WorkingState { get; init; } = new();
    /// <summary>Gets the stable source event identity.</summary>
    [Key(9)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the source event sequence.</summary>
    [Key(10)] public long SourceEventSequence { get; init; }
    /// <summary>Gets the source event contract name.</summary>
    [Key(11)] public string SourceEventName { get; init; } = string.Empty;

    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookComponentObservedEvent);
    /// <summary>Gets the domain event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <inheritdoc/>
    public ICompleteEvent<TId> ToCompleteEvent<TComplete, TId>()
        where TComplete : ICompleteEvent<TId>
        where TId : IActorEntityId
    {
        EnsureEntityId<TId>();
        ICompleteEvent<MarketOutlookEntityId> completed = new MarketOutlookComponentObservedCompleteEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentObservedCompleteEvent.Verb, EntityId),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            WorkingState = WorkingState,
            SourceEventId = SourceEventId,
            SourceEventSequence = SourceEventSequence,
            SourceEventName = SourceEventName
        };
        return (ICompleteEvent<TId>)completed;
    }

    /// <inheritdoc/>
    public IErrorEvent<TId> ToFailEvent<TFail, TId>(Exception exception)
        where TFail : IErrorEvent<TId>
        where TId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(exception);
        EnsureEntityId<TId>();
        IErrorEvent<MarketOutlookEntityId> failed = new MarketOutlookComponentObservedFailEvent
        {
            Subject = RealtimeSubject(MarketOutlookComponentObservedFailEvent.Verb, EntityId),
            EntityId = EntityId,
            Id = Id,
            ErrorDate = DateTime.UtcNow,
            EventId = EventId,
            CommandId = CommandId == Guid.Empty ? Guid.NewGuid() : CommandId,
            EventSource = EventSource,
            ErrorMessage = exception.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = exception.ToString(),
            ReceivedOn = ReceivedOn,
            AggregateId = AggregateId
        };
        return (IErrorEvent<TId>)failed;
    }

    static void EnsureEntityId<TId>() where TId : IActorEntityId
    {
        if (typeof(TId) != typeof(MarketOutlookEntityId))
            throw new InvalidOperationException($"Unsupported entity id {typeof(TId).FullName} for {nameof(MarketOutlookComponentObservedEvent)}.");
    }

    static ActorSubject RealtimeSubject(string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Realtime, MarketOutlookComponentChangedRealtimeEvent.Actor, verb, entityId.Format());
}

/// <summary>Confirms successful projection of a Market Outlook component transition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookComponentObservedCompleteEvent : ICompleteEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = MarketOutlookComponentChangedRealtimeEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "ComponentObservedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the projected working state.</summary>
    [Key(8)] public MarketOutlookWorkingStateReadModel WorkingState { get; init; } = new();
    /// <summary>Gets the stable source event identity.</summary>
    [Key(9)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the source event sequence.</summary>
    [Key(10)] public long SourceEventSequence { get; init; }
    /// <summary>Gets the source event contract name.</summary>
    [Key(11)] public string SourceEventName { get; init; } = string.Empty;
    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookComponentObservedCompleteEvent);
    /// <summary>Gets the completed event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports failure to project a Market Outlook component transition.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookComponentObservedFailEvent : IErrorEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = MarketOutlookComponentChangedRealtimeEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "ComponentObservedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookComponentObservedFailEvent);
    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the error event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}

/// <summary>Records publication of a finalized Market Outlook snapshot and its aggregate checkpoint.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotPublishedEvent : IEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = MarketOutlookComponentObservedEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "SnapshotPublished";
    /// <summary>Gets the projection error code.</summary>
    [IgnoreMember] public const int ErrorCode = 19102;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the full published aggregate checkpoint.</summary>
    [Key(8)] public MarketOutlookWorkingStateReadModel WorkingState { get; init; } = new();
    /// <summary>Gets the finalized UI/query snapshot.</summary>
    [Key(9)] public MarketOutlookSnapshotReadModel MarketOutlook { get; init; } = new();
    /// <summary>Gets the stable source EOD event identity.</summary>
    [Key(10)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookSnapshotPublishedEvent);
    /// <summary>Gets the domain event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    /// <inheritdoc/>
    public ICompleteEvent<TId> ToCompleteEvent<TComplete, TId>()
        where TComplete : ICompleteEvent<TId>
        where TId : IActorEntityId
    {
        EnsureEntityId<TId>();
        ICompleteEvent<MarketOutlookEntityId> completed = new MarketOutlookSnapshotPublishedCompleteEvent
        {
            Subject = RealtimeSubject(MarketOutlookSnapshotPublishedCompleteEvent.Verb, EntityId),
            EntityId = EntityId,
            Id = Id,
            EventId = EventId,
            CommandId = CommandId,
            AggregateId = AggregateId,
            EventSource = EventSource,
            ReceivedOn = ReceivedOn,
            WorkingState = WorkingState,
            MarketOutlook = MarketOutlook,
            SourceEventId = SourceEventId
        };
        return (ICompleteEvent<TId>)completed;
    }

    /// <inheritdoc/>
    public IErrorEvent<TId> ToFailEvent<TFail, TId>(Exception exception)
        where TFail : IErrorEvent<TId>
        where TId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(exception);
        EnsureEntityId<TId>();
        IErrorEvent<MarketOutlookEntityId> failed = new MarketOutlookSnapshotPublishedFailEvent
        {
            Subject = RealtimeSubject(MarketOutlookSnapshotPublishedFailEvent.Verb, EntityId),
            EntityId = EntityId,
            Id = Id,
            ErrorDate = DateTime.UtcNow,
            EventId = EventId,
            CommandId = CommandId == Guid.Empty ? Guid.NewGuid() : CommandId,
            EventSource = EventSource,
            ErrorMessage = exception.Message,
            ErrorType = ErrorType.Command,
            ErrorCode = ErrorCode,
            ErrorData = exception.ToString(),
            ReceivedOn = ReceivedOn,
            AggregateId = AggregateId
        };
        return (IErrorEvent<TId>)failed;
    }

    static void EnsureEntityId<TId>() where TId : IActorEntityId
    {
        if (typeof(TId) != typeof(MarketOutlookEntityId))
            throw new InvalidOperationException($"Unsupported entity id {typeof(TId).FullName} for {nameof(MarketOutlookSnapshotPublishedEvent)}.");
    }

    static ActorSubject RealtimeSubject(string verb, MarketOutlookEntityId entityId)
        => new(ActorType.Realtime, MarketOutlookComponentChangedRealtimeEvent.Actor, verb, entityId.Format());
}

/// <summary>Confirms successful projection of a published Market Outlook snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotPublishedCompleteEvent : ICompleteEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = MarketOutlookComponentChangedRealtimeEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "SnapshotPublishedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    /// <summary>Gets the projected aggregate checkpoint.</summary>
    [Key(8)] public MarketOutlookWorkingStateReadModel WorkingState { get; init; } = new();
    /// <summary>Gets the projected UI/query snapshot.</summary>
    [Key(9)] public MarketOutlookSnapshotReadModel MarketOutlook { get; init; } = new();
    /// <summary>Gets the stable source EOD event identity.</summary>
    [Key(10)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookSnapshotPublishedCompleteEvent);
    /// <summary>Gets the completed event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;
}

/// <summary>Reports failure to project a published Market Outlook snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotPublishedFailEvent : IErrorEvent<MarketOutlookEntityId>
{
    /// <summary>Gets the event actor name.</summary>
    [IgnoreMember] public const string Actor = MarketOutlookComponentChangedRealtimeEvent.Actor;
    /// <summary>Gets the event verb.</summary>
    [IgnoreMember] public const string Verb = "SnapshotPublishedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public DateTime ErrorDate { get; init; }
    [Key(4)] public long EventId { get; init; }
    [Key(5)] public Guid CommandId { get; init; }
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(8)] public int ErrorCode { get; init; }
    [Key(9)] public ErrorType ErrorType { get; init; }
    [Key(10)] public string ErrorData { get; init; } = string.Empty;
    [Key(11)] public DateTime ReceivedOn { get; init; }
    [Key(12)] public string AggregateId { get; init; } = string.Empty;
    [Key(13)] public string CommandName { get; init; } = string.Empty;
    [Key(14)] public string CommandData { get; init; } = string.Empty;
    /// <summary>Gets the CLR event name.</summary>
    [IgnoreMember] public string EventName => nameof(MarketOutlookSnapshotPublishedFailEvent);
    /// <summary>Gets the user associated with the event.</summary>
    [IgnoreMember] public string UserName => string.Empty;
    /// <summary>Gets the error event classification.</summary>
    [IgnoreMember] public EventType EventType => EventType.ErrorEvent;
}
