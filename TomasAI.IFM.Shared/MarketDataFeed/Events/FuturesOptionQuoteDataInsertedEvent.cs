using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event indicating that futures option quote data has been inserted.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataInsertedEvent : IEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "Inserted";
    [IgnoreMember] public const int ErrorCode = 6008;

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
    [Key(9)] public string ContractId { get; init; }
    [Key(10)] public FuturesOptionQuoteDataReadModel OptionQuoteData { get; init; }
    [Key(11)] public DateTime UpdatedOn { get; init; }
    [Key(12)] public string UpdatedBy { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesOptionQuoteDataInsertedEvent() { }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionQuoteDataInsertedEvent(
        ActorSubject subject,
        Guid id,
        QuoteId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int quoteId,
        string contractId,
        FuturesOptionQuoteDataReadModel optionQuoteData,
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
        QuoteId = quoteId;
        ContractId = contractId ?? string.Empty;
        OptionQuoteData = optionQuoteData;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
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

        ICompleteEvent<QuoteId> completed = new FuturesOptionQuoteDataInsertedCompleteEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataInsertedCompleteEvent.Actor, FuturesOptionQuoteDataInsertedCompleteEvent.Verb, this.Subject.EntityId),
            EntityId = this.EntityId,
            Id = this.Id,
            EventId = this.EventId,
            CommandId = this.CommandId,
            AggregateId = this.AggregateId,
            EventSource = this.EventSource,
            ReceivedOn = this.ReceivedOn,
            QuoteId = this.QuoteId,
            OptionQuoteData = this.OptionQuoteData,
            UpdatedOn = this.UpdatedOn,
            UpdatedBy = this.UpdatedBy
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

        IErrorEvent<QuoteId> failed = new FuturesOptionQuoteDataInsertedFailEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionQuoteDataInsertedFailEvent.Actor, FuturesOptionQuoteDataInsertedFailEvent.Verb, this.Subject.EntityId),
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
/// Event published when futures option quote data has been inserted successfully.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataInsertedCompleteEvent : ICompleteEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "InsertedComplete";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public QuoteId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    [Key(8)] public int QuoteId { get; init; }
    [Key(9)] public FuturesOptionQuoteDataReadModel OptionQuoteData { get; init; }
    [Key(10)] public DateTime UpdatedOn { get; init; }
    [Key(11)] public string UpdatedBy { get; init; }
    [Key(12)] public int ErrorCode { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.CompletedEvent;

    public FuturesOptionQuoteDataInsertedCompleteEvent() { }

    [SerializationConstructor]
    public FuturesOptionQuoteDataInsertedCompleteEvent(
        ActorSubject subject,
        QuoteId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int quoteId,
        FuturesOptionQuoteDataReadModel optionQuoteData,
        DateTime updatedOn,
        string updatedBy,
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
        OptionQuoteData = optionQuoteData;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Event published when inserting futures option quote data fails.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataInsertedFailEvent : IErrorEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "InsertedFail";

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

    public FuturesOptionQuoteDataInsertedFailEvent() { }

    [SerializationConstructor]
    public FuturesOptionQuoteDataInsertedFailEvent(
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
