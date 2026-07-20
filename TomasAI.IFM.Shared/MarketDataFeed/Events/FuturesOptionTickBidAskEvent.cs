using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event that conveys real-time bid and ask data for a futures option, including associated
/// metadata and tick price information.
/// </summary>
/// <remarks>This event is typically published by a market data feed to notify consumers of updates to bid and ask
/// prices for a specific futures option. It includes identifiers, timestamps, and contextual information to facilitate
/// tracking and correlation within event-driven systems. Consumers can use this event to react to market changes,
/// update user interfaces, or trigger automated trading logic.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickBidAskEvent : IEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "FuturesOptionTickDataEvent";
    [IgnoreMember] public const string Verb = "TickPriceData";

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int RequestId { get; init; }
    [Key(9)] public FuturesOptionTickBidAskReadModel TickBidAskData { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesOptionTickBidAskEvent() { }

    public FuturesOptionTickBidAskEvent(int requestId, FuturesOptionTickBidAskReadModel tickPriceData) 
    { 
        RequestId = requestId;
        TickBidAskData = tickPriceData;
    }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesOptionTickBidAskEvent(
        ActorSubject subject,
        FuturesOptionTickEntityId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int requestId,
        FuturesOptionTickBidAskReadModel tickPriceData)
    {
        Subject = subject;
        EntityId = entityId;
        Id = id;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        RequestId = requestId;
        TickBidAskData = tickPriceData;
    }
}
