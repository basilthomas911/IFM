using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event indicating that the market data feed has been started.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataFeedStartedEvent : IEvent<MarketDataFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "Started";
    [IgnoreMember] public const int ErrorCode = 5010;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public MarketDataFeedId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FuturesContractV2ReadModel[]? FuturesContracts { get; init; }
    [Key(9)] public DateOnly ValueDate { get; init; }
    [Key(10)] public bool ResetStream { get; init; }
    [Key(11)] public DateTime StartedOn { get; init; }
    [Key(12)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public MarketDataFeedStartedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public MarketDataFeedStartedEvent(
        ActorSubject subject,
        Guid id,
        MarketDataFeedId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV2ReadModel[]? futuresContracts,
        DateOnly valueDate,
        bool resetStream,
        DateTime startedOn,
        string startedBy)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesContracts = futuresContracts;
        ValueDate = valueDate;
        ResetStream = resetStream;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
    }

    /// <summary>
    /// Convert this denormalize event into a completed event which indicates successful handling.
    /// </summary>
    public ICompleteEvent<TEntityId> ToCompleteEvent<TComplete, TEntityId>()
        where TComplete : ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(MarketDataFeedId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(MarketDataFeedId).FullName}.");

        ICompleteEvent<MarketDataFeedId> completed = new MarketDataFeedStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedStartedCompleteEvent.Actor, MarketDataFeedStartedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            FuturesContracts = this.FuturesContracts,
            ValueDate = this.ValueDate,
            ResetStream = this.ResetStream,
            StartedOn = this.StartedOn,
            StartedBy = this.StartedBy
        };

        return (ICompleteEvent<TEntityId>)completed;
    }

    /// <summary>
    /// Convert this denormalize event into a failed error event describing the provided exception.
    /// </summary>
    public IErrorEvent<TEntityId> ToFailEvent<TFail, TEntityId>(Exception ex)
        where TFail : IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (typeof(TEntityId) != typeof(MarketDataFeedId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(MarketDataFeedId).FullName}.");

        IErrorEvent<MarketDataFeedId> failed = new MarketDataFeedStartedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, MarketDataFeedStartedFailEvent.Actor, MarketDataFeedStartedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when a market data feed has been started successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataFeedStartedCompleteEvent : ICompleteEvent<MarketDataFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "StartedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketDataFeedId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesContractV2ReadModel[]? FuturesContracts { get; init; }
    [Key(9)] public DateOnly ValueDate { get; init; }
    [Key(10)] public bool ResetStream { get; init; }
    [Key(11)] public DateTime StartedOn { get; init; }
    [Key(12)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public MarketDataFeedStartedCompleteEvent() { }

    [SerializationConstructor]
    public MarketDataFeedStartedCompleteEvent(
        ActorSubject subject,
        MarketDataFeedId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesContractV2ReadModel[]? futuresContracts,
        DateOnly valueDate,
        bool resetStream,
        DateTime startedOn,
        string startedBy)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        FuturesContracts = futuresContracts;
        ValueDate = valueDate;
        ResetStream = resetStream;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when starting a market data feed fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataFeedStartedFailEvent : IErrorEvent<MarketDataFeedId>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedEvent";
    [IgnoreMember] public const string Verb = "StartedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public MarketDataFeedId EntityId { get; init; }
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

    public MarketDataFeedStartedFailEvent() { }

    [SerializationConstructor]
    public MarketDataFeedStartedFailEvent(
        ActorSubject subject,
        MarketDataFeedId entityId,
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
