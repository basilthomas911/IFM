using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Describes the latest normalized futures trade state carried by a realtime market-price update.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesMarketTradeSnapshot(
    [property: Key(0)] decimal LastPrice,
    [property: Key(1)] uint LastSize,
    [property: Key(2)] long SourceSequence,
    [property: Key(3)] DateTimeOffset EventTimestamp,
    [property: Key(4)] DateTimeOffset ReceiveTimestamp);

/// <summary>
/// Describes the latest normalized futures quote state carried by a realtime market-price update.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesMarketQuoteSnapshot(
    [property: Key(0)] decimal? BidPrice,
    [property: Key(1)] uint BidSize,
    [property: Key(2)] decimal? AskPrice,
    [property: Key(3)] uint AskSize,
    [property: Key(4)] uint BidCount,
    [property: Key(5)] uint AskCount,
    [property: Key(6)] long SourceSequence,
    [property: Key(7)] DateTimeOffset EventTimestamp,
    [property: Key(8)] DateTimeOffset ReceiveTimestamp);

/// <summary>
/// Provides a provider-neutral futures price snapshot for actor-domain realtime processing.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesMarketPriceSnapshot(
    [property: Key(0)] string ContractId,
    [property: Key(1)] uint InstrumentId,
    [property: Key(2)] ushort PublisherId,
    [property: Key(3)] AssetTypeId AssetTypeId,
    [property: Key(4)] DateOnly ValueDate,
    [property: Key(5)] FuturesMarketQuoteSnapshot? Quote,
    [property: Key(6)] FuturesMarketTradeSnapshot? Trade);

/// <summary>
/// Carries one non-durable update to the primary futures market-price realtime actor and its registered routes.
/// </summary>
[MessagePackObject]
public sealed record FuturesMarketPriceUpdatedRealtimeEvent : IEvent<TickDataEntityId>
{
    /// <summary>The primary actor mailbox name used by the realtime subject.</summary>
    public const string Actor = "FuturesMarketPrice";

    /// <summary>The realtime event verb.</summary>
    public const string Verb = "Updated";

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public TickDataEntityId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;
    [Key(9)] public FuturesMarketPriceSnapshot Price { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesMarketPriceUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
