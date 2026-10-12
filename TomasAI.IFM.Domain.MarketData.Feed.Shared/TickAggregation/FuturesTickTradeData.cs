using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesTickTradeData(
    [property: Key(0)] uint SourceSequence,
    [property: Key(1)] long EventTimestampNanoseconds,
    [property: Key(2)] long ReceiveTimestampNanoseconds,
    [property: Key(3)] byte HeaderFlags,
    [property: Key(4)] long PriceRaw,
    [property: Key(5)] decimal Price,
    [property: Key(6)] uint Size,
    [property: Key(7)] byte Action,
    [property: Key(8)] byte Side,
    [property: Key(9)] byte DbnFlags);
