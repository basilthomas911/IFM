using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject(AllowPrivate = true)]
public sealed record FuturesTickQuoteDataInsertedEvent : IEvent<TickDataEntityId>
{

    /// <summary>Creates an empty event for serialization.</summary>
    public FuturesTickQuoteDataInsertedEvent() { }

    /// <summary>Rehydrates every published event field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="id">The Id field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="eventId">The EventId field.</param>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="aggregateId">The AggregateId field.</param>
    /// <param name="eventSource">The EventSource field.</param>
    /// <param name="receivedOn">The ReceivedOn field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="tickDataId">The TickDataId field.</param>
    /// <param name="assetTypeId">The AssetTypeId field.</param>
    /// <param name="dataset">The Dataset field.</param>
    /// <param name="definitionDate">The DefinitionDate field.</param>
    /// <param name="publisherId">The PublisherId field.</param>
    /// <param name="instrumentId">The InstrumentId field.</param>
    /// <param name="emissionReason">The EmissionReason field.</param>
    /// <param name="quoteCount">The QuoteCount field.</param>
    /// <param name="quoteData">The QuoteData field.</param>
    [SerializationConstructor]
    public FuturesTickQuoteDataInsertedEvent(ActorSubject subject, Guid id, TickDataEntityId entityId, long eventId, Guid commandId, string aggregateId, string eventSource, DateTime receivedOn, ushort schemaVersion, TickDataId tickDataId, AssetTypeId assetTypeId, string dataset, DateOnly definitionDate, ushort publisherId, uint instrumentId, QuoteEmissionReason emissionReason, ushort quoteCount, FuturesTickQuoteDataSegment quoteData)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId;
        EventSource = eventSource;
        ReceivedOn = receivedOn;
        SchemaVersion = schemaVersion;
        TickDataId = tickDataId;
        AssetTypeId = assetTypeId;
        Dataset = dataset;
        DefinitionDate = definitionDate;
        PublisherId = publisherId;
        InstrumentId = instrumentId;
        EmissionReason = emissionReason;
        QuoteCount = quoteCount;
        QuoteData = quoteData;
    }
    public const string Actor = "TickAggregationRealtime";
    public const string Verb = "FuturesTickQuoteDataInserted";
    public const int ErrorId = 5704;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TickDataEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;
    [Key(9)] public TickDataId TickDataId { get; init; }
    [Key(10)] public AssetTypeId AssetTypeId { get; init; }
    [Key(11)] public string Dataset { get; init; } = string.Empty;
    [Key(12)] public DateOnly DefinitionDate { get; init; }
    [Key(13)] public ushort PublisherId { get; init; }
    [Key(14)] public uint InstrumentId { get; init; }
    [Key(15)] public QuoteEmissionReason EmissionReason { get; init; }
    [Key(16)] public ushort QuoteCount { get; init; }
    [Key(17)] public FuturesTickQuoteDataSegment QuoteData { get; init; }
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesTickQuoteDataInsertedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public ICompleteEvent<TId> ToCompleteEvent<TComplete, TId>()
        where TComplete : ICompleteEvent<TId> where TId : IActorEntityId
    {
        if (typeof(TId) != typeof(TickDataEntityId) || typeof(TComplete) != typeof(FuturesTickQuoteDataInsertedCompleteEvent))
            throw new InvalidOperationException("The requested completion event type does not match the quote event family.");
        object result = TickAggregationEventFactory.Complete(this, QuoteCount, FuturesTickQuoteDataInsertedCompleteEvent.Verb);
        return (ICompleteEvent<TId>)result;
    }

    public IErrorEvent<TId> ToFailEvent<TFail, TId>(Exception ex)
        where TFail : IErrorEvent<TId> where TId : IActorEntityId
    {
        if (typeof(TId) != typeof(TickDataEntityId) || typeof(TFail) != typeof(FuturesTickQuoteDataInsertedFailEvent))
            throw new InvalidOperationException("The requested failure event type does not match the quote event family.");
        object result = TickAggregationEventFactory.Fail(this, ex, QuoteCount, FuturesTickQuoteDataInsertedFailEvent.Verb, ErrorId);
        return (IErrorEvent<TId>)result;
    }
}

[MessagePackObject]
public record FuturesTickQuoteDataInsertedCompleteEvent : TickAggregationCompleteEvent
{
    public new const string Verb = "FuturesTickQuoteDataInsertedComplete";
}

[MessagePackObject]
public record FuturesTickQuoteDataInsertedFailEvent : TickAggregationFailEvent
{
    public new const string Verb = "FuturesTickQuoteDataInsertedFail";
}
