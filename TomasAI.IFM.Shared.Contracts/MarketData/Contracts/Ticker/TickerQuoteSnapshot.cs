using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

/// <summary>
/// Latest quote state for one ticker, expressed entirely in actor-domain values.
/// </summary>
/// <param name="BidPrice">The best bid price.</param>
/// <param name="BidSize">The quantity available at the best bid.</param>
/// <param name="AskPrice">The best ask price.</param>
/// <param name="AskSize">The quantity available at the best ask.</param>
/// <param name="BidCount">The number of orders at the best bid.</param>
/// <param name="AskCount">The number of orders at the best ask.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct TickerQuoteSnapshot(
    decimal? BidPrice,
    uint BidSize,
    decimal? AskPrice,
    uint AskSize,
    uint BidCount,
    uint AskCount,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);
