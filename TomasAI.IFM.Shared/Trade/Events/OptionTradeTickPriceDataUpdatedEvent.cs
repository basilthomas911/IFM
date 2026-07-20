using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Events;

/// <summary>
/// Represents a domain event indicating that futures option tick data has been updated.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeTickPriceDataUpdatedEvent : IEvent<FuturesOptionTickEntityId>
{
    [IgnoreMember] public const string Actor = "OptionTradeEvent";
    [IgnoreMember] public const string Verb = "TickPriceDataUpdated";

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
    [Key(8)] public FuturesOptionTickDataV2ReadModel OptionTickData { get; init; }

    [IgnoreMember] public string UserName => $"{Environment.UserDomainName}\\{Environment.UserName}";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;

    public OptionTradeTickPriceDataUpdatedEvent() { }
    public OptionTradeTickPriceDataUpdatedEvent(FuturesOptionTickDataV2ReadModel optionTickData) 
    {
        OptionTickData = IsArgumentNull.Set(optionTickData);
    }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public OptionTradeTickPriceDataUpdatedEvent(
        ActorSubject subject,
        Guid id,
        FuturesOptionTickEntityId entityId,
        long eventId,
        Guid commandId,
        string aggregateId,
        string eventSource,
        DateTime receivedOn,
        FuturesOptionTickDataV2ReadModel optionTickData)
    {
        Subject = subject;
        Id = id;
        EntityId = entityId;
        EventId = eventId;
        CommandId = commandId;
        AggregateId = aggregateId ?? string.Empty;
        EventSource = eventSource ?? string.Empty;
        ReceivedOn = receivedOn;
        OptionTickData = optionTickData;
    }
}
