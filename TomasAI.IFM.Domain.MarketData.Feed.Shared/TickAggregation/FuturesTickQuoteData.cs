using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesTickQuoteData(
    [property: Key(0)] uint SourceSequence,
    [property: Key(1)] long EventTimestampNanoseconds,
    [property: Key(2)] long ReceiveTimestampNanoseconds,
    [property: Key(3)] byte HeaderFlags,
    [property: Key(4)] long BidPriceRaw,
    [property: Key(5)] decimal? BidPrice,
    [property: Key(6)] uint BidSize,
    [property: Key(7)] uint BidCount,
    [property: Key(8)] long AskPriceRaw,
    [property: Key(9)] decimal? AskPrice,
    [property: Key(10)] uint AskSize,
    [property: Key(11)] uint AskCount);
