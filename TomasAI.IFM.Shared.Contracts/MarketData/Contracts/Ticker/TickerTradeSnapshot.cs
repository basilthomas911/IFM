using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

/// <summary>
/// Latest trade state for one ticker, expressed entirely in actor-domain values.
/// </summary>
/// <param name="LastPrice">The last accepted trade price.</param>
/// <param name="LastSize">The quantity of the last accepted trade.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct TickerTradeSnapshot(
    decimal LastPrice,
    uint LastSize,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);
