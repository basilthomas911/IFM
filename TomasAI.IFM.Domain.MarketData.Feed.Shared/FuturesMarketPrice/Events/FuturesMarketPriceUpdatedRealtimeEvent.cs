using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Identifies which normalized observation advanced a realtime futures-price snapshot.
/// </summary>
public enum FuturesMarketPriceUpdateSource : byte
{
    Unknown = 0,
    Quote = 1,
    Trade = 2
}

/// <summary>
/// Describes the provider-neutral lifecycle action represented by a normalized trade.
/// </summary>
public enum NormalizedTradeAction : byte
{
    /// <summary>The source action was absent or unsupported.</summary>
    Unknown = 0,

    /// <summary>A new trade was reported.</summary>
    New = 1,

    /// <summary>An existing trade was modified or corrected.</summary>
    Change = 2,

    /// <summary>An existing trade was cancelled.</summary>
    Cancel = 3,

    /// <summary>The source explicitly reported a correction.</summary>
    Correct = 4,

    /// <summary>The source cleared its current trade state.</summary>
    Clear = 5,

    /// <summary>The source explicitly supplied no action.</summary>
    None = 6
}

/// <summary>
/// Describes the provider-neutral aggressor side of a normalized trade.
/// </summary>
public enum NormalizedTradeSide : byte
{
    /// <summary>The source side was absent or unsupported.</summary>
    Unknown = 0,

    /// <summary>The aggressor bought at the reported price.</summary>
    Buy = 1,

    /// <summary>The aggressor sold at the reported price.</summary>
    Sell = 2,

    /// <summary>The source explicitly supplied no aggressor side.</summary>
    Unspecified = 3
}

/// <summary>
/// Provides provider-neutral conditions retained from a normalized trade observation.
/// </summary>
[Flags]
public enum NormalizedTradeConditionFlags : ushort
{
    /// <summary>No normalized condition was reported.</summary>
    None = 0,

    /// <summary>The trade was the last record in its source event.</summary>
    LastInEvent = 1 << 0,

    /// <summary>The trade represented top-of-book information.</summary>
    TopOfBook = 1 << 1,

    /// <summary>The source marked the observation as a snapshot.</summary>
    Snapshot = 1 << 2,

    /// <summary>The observation was delivered during source replay.</summary>
    Replay = 1 << 3,

    /// <summary>The source aggregated multiple orders at one price level.</summary>
    AggregatedPriceLevel = 1 << 4,

    /// <summary>The source receive timestamp may be inaccurate.</summary>
    ReceiveTimestampInaccurate = 1 << 5,

    /// <summary>The source indicated that its order-book state may be inaccurate.</summary>
    BookMayBeInaccurate = 1 << 6,

    /// <summary>The condition contains publisher-specific semantics.</summary>
    PublisherSpecific = 1 << 7,

    /// <summary>The source supplied an undefined trade price.</summary>
    UndefinedPrice = 1 << 8
}

/// <summary>
/// Describes the latest normalized futures trade state carried by a realtime market-price update.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesMarketTradeSnapshot(
    [property: Key(0)] decimal LastPrice,
    [property: Key(1)] uint LastSize,
    [property: Key(2)] long SourceSequence,
    [property: Key(3)] DateTimeOffset EventTimestamp,
    [property: Key(4)] DateTimeOffset ReceiveTimestamp,
    [property: Key(5)] NormalizedTradeAction NormalizedTradeAction = NormalizedTradeAction.Unknown,
    [property: Key(6)] NormalizedTradeSide NormalizedTradeSide = NormalizedTradeSide.Unknown,
    [property: Key(7)] NormalizedTradeConditionFlags NormalizedTradeConditionFlags = NormalizedTradeConditionFlags.None,
    [property: Key(8)] Guid StreamEpochId = default,
    [property: Key(9)] long TradeOrdinal = 0);

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
    [Key(8)] public ushort SchemaVersion { get; init; } = 2;
    [Key(9)] public FuturesMarketPriceSnapshot Price { get; init; }
    [Key(10)] public FuturesMarketPriceUpdateSource UpdateSource { get; init; }

    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(FuturesMarketPriceUpdatedRealtimeEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}
