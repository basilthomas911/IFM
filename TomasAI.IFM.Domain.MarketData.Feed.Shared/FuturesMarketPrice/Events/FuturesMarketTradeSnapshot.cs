using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Describes the latest normalized futures trade state carried by a realtime market-price update.
/// </summary>
[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
