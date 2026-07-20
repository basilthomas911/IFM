using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event indicating that futures option quote data streaming has been started.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataStreamingStartedEvent : IEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStarted";
    [IgnoreMember] public const int ErrorCode = 6006;

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public QuoteId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int QuoteId { get; init; }
    [Key(9)] public FuturesOptionQuoteReadModel[] FuturesOptionQuotes { get; init; }
    [Key(10)] public FuturesOptionContractReadModel[] FuturesOptionContracts { get; init; }
    [Key(11)] public FuturesOptionQuoteDataReadModel[] FuturesOptionQuoteData { get; init; }
    [Key(12)] public DateTime StartedOn { get; init; }
    [Key(13)] public string StartedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesOptionQuoteDataStreamingStartedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionQuoteDataStreamingStartedEvent(
        ActorSubject subject,
        Guid id,
        QuoteId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int quoteId,
        FuturesOptionQuoteReadModel[] futuresOptionQuotes,
        FuturesOptionContractReadModel[] futuresOptionContracts,
        FuturesOptionQuoteDataReadModel[] futuresOptionQuoteData,
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
        QuoteId = quoteId;
        FuturesOptionQuotes = futuresOptionQuotes;
        FuturesOptionContracts = futuresOptionContracts;
        FuturesOptionQuoteData = futuresOptionQuoteData;
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
        if (typeof(TEntityId) != typeof(QuoteId))
            throw new InvalidOperationException($"{EventName}.ToCompleteEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(QuoteId).FullName}.");

        ICompleteEvent<QuoteId> completed = new FuturesOptionQuoteDataStreamingStartedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataStreamingStartedCompleteEvent.Actor, FuturesOptionQuoteDataStreamingStartedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            QuoteId = this.QuoteId,
            FuturesOptionQuotes = this.FuturesOptionQuotes,
            FuturesOptionContracts = this.FuturesOptionContracts,
            FuturesOptionQuoteData = this.FuturesOptionQuoteData,
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
        if (typeof(TEntityId) != typeof(QuoteId))
            throw new InvalidOperationException($"{EventName}.ToFailEvent: unsupported entity id type {typeof(TEntityId).FullName}. Expected {typeof(QuoteId).FullName}.");

        IErrorEvent<QuoteId> failed = new FuturesOptionQuoteDataStreamingStartedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataStreamingStartedFailEvent.Actor, FuturesOptionQuoteDataStreamingStartedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when futures option quote data streaming has been started successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataStreamingStartedCompleteEvent : ICompleteEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStartedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public QuoteId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int QuoteId { get; init; }
    [Key(9)] public FuturesOptionQuoteReadModel[] FuturesOptionQuotes { get; init; }
    [Key(10)] public FuturesOptionContractReadModel[] FuturesOptionContracts { get; init; }
    [Key(11)] public FuturesOptionQuoteDataReadModel[] FuturesOptionQuoteData { get; init; }
    [Key(12)] public DateTime StartedOn { get; init; }
    [Key(13)] public string StartedBy { get; init; }
    [Key(14)] public int ErrorCode { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesOptionQuoteDataStreamingStartedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesOptionQuoteDataStreamingStartedCompleteEvent(
        ActorSubject subject,
        QuoteId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int quoteId,
        FuturesOptionQuoteReadModel[] futuresOptionQuotes,
        FuturesOptionContractReadModel[] futuresOptionContracts,
        FuturesOptionQuoteDataReadModel[] futuresOptionQuoteData,
        DateTime startedOn,
        string startedBy,
        int errorCode)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        QuoteId = quoteId;
        FuturesOptionQuotes = futuresOptionQuotes;
        FuturesOptionContracts = futuresOptionContracts;
        FuturesOptionQuoteData = futuresOptionQuoteData;
        StartedOn = startedOn;
        StartedBy = startedBy ?? string.Empty;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Event published when starting futures option quote data streaming fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataStreamingStartedFailEvent : IErrorEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "StreamingStartedFail";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public QuoteId EntityId { get; init; }
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

    public FuturesOptionQuoteDataStreamingStartedFailEvent() { }

    [SerializationConstructor]
    public FuturesOptionQuoteDataStreamingStartedFailEvent(
        ActorSubject subject,
        QuoteId entityId,
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
