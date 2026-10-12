using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Describes the latest normalized futures quote state carried by a realtime market-price update.
/// </summary>
[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
