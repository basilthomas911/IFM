using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Event published when futures option quote data has been updated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataUpdatedEvent : IEvent<QuoteId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionQuoteDataEvent";
    [IgnoreMember] public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public QuoteId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public FuturesOptionQuoteDataReadModel OptionQuoteData { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesOptionQuoteDataUpdatedEvent() { }

    public FuturesOptionQuoteDataUpdatedEvent(FuturesOptionQuoteDataReadModel optionQuoteData) 
    {
        OptionQuoteData = optionQuoteData;
    }

    [SerializationConstructor]
    public FuturesOptionQuoteDataUpdatedEvent(
        ActorSubject subject,
        QuoteId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionQuoteDataReadModel optionQuoteData)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OptionQuoteData = optionQuoteData;
    }
}
