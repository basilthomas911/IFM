using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events;

/// <summary>
/// Represents a domain event that conveys real-time bid and ask data for a futures contract, including associated
/// metadata and tick price information.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTickBidAskEvent : IEvent<FeedId>
{
    [IgnoreMember] public const string Actor = "FuturesTickDataEvent";
    [IgnoreMember] public const string Verb = "TickBidAskData";

    // base metadata (keys 0..7)
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public FeedId EntityId { get; init; }
    [Key(2)] public Guid Id { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; }
    [Key(6)] public string EventSource { get; init; }
    [Key(7)] public DateTime ReceivedOn { get; init; }

    // payload (keys 8..)
    [Key(8)] public int RequestId { get; init; }
    [Key(9)] public FuturesTickBidAskReadModel TickBidAskData { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => nameof(FuturesTickBidAskEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public FuturesTickBidAskEvent() { }

    public FuturesTickBidAskEvent(int requestId, FuturesTickBidAskReadModel tickBidAskData)
    {
        RequestId = requestId;
        TickBidAskData = tickBidAskData;
    }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FuturesTickBidAskEvent(
        ActorSubject subject,
        FeedId entityId,
        Guid id,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        int requestId,
        FuturesTickBidAskReadModel tickBidAskData)
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
        TickBidAskData = tickBidAskData;
    }

    [IgnoreMember]
    public bool IsValid
        => EntityId.IsValid && TickBidAskData.IsValid;
}
