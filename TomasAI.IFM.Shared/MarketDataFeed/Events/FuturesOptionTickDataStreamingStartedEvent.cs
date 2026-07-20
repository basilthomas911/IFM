using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event indicating that futures option tick data streaming has been started.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickDataStreamingStartedEvent : IEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTickDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStarted";
    [IgnoreMember] public const int ErrorCode = 6006;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(10)] public FuturesContractV2ReadModel BaseContract { get; init; }
    [Key(11)] public DateOnly ValueDate { get; init; }
    [Key(12)] public DateOnly MaturityDate { get; init; }
    [Key(13)] public double RiskFreeRate { get; init; }
    [Key(14)] public DateTime StartedOn { get; init; }
    [Key(15)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesOptionTickDataStreamingStartedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionTickDataStreamingStartedEvent(
        ActorSubject subject,
        Guid id,
        FuturesOptionTickEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel contract,
        FuturesContractV2ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate,
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
        Contract = contract;
        BaseContract = baseContract;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
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
        if (typeof(TEntityId) != typeof(FuturesOptionTickEntityId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionTickEntityId).FullName}.");

        ICompleteEvent<FuturesOptionTickEntityId> completed = new FuturesOptionTickDataStreamingStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataStreamingStartedCompleteEvent.Actor, FuturesOptionTickDataStreamingStartedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            Contract = this.Contract,
            BaseContract = this.BaseContract,
            ValueDate = this.ValueDate,
            MaturityDate = this.MaturityDate,
            RiskFreeRate = this.RiskFreeRate,
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
        if (typeof(TEntityId) != typeof(FuturesOptionTickEntityId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(FuturesOptionTickEntityId).FullName}.");

        IErrorEvent<FuturesOptionTickEntityId> failed = new FuturesOptionTickDataStreamingStartedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionTickDataStreamingStartedFailEvent.Actor, FuturesOptionTickDataStreamingStartedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when futures option tick data streaming has been started successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickDataStreamingStartedCompleteEvent : ICompleteEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTickDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStartedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(9)] public FuturesContractV2ReadModel BaseContract { get; init; }
    [Key(10)] public DateOnly ValueDate { get; init; }
    [Key(11)] public DateOnly MaturityDate { get; init; }
    [Key(12)] public double RiskFreeRate { get; init; }
    [Key(13)] public DateTime StartedOn { get; init; }
    [Key(14)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesOptionTickDataStreamingStartedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesOptionTickDataStreamingStartedCompleteEvent(
        ActorSubject subject,
        FuturesOptionTickEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionContractReadModel contract,
        FuturesContractV2ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate,
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
        Contract = contract;
        BaseContract = baseContract;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
    }
}

/// <summary>
/// Event published when starting futures option tick data streaming fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickDataStreamingStartedFailEvent : IErrorEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTickDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStartedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionTickEntityId EntityId { get; init; }
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

    public FuturesOptionTickDataStreamingStartedFailEvent() { }

    [SerializationConstructor]
    public FuturesOptionTickDataStreamingStartedFailEvent(
        ActorSubject subject,
        FuturesOptionTickEntityId entityId,
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
