using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero represents no valid persisted business choice; adding it would introduce an unsupported value.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum QuoteEmissionReason : byte
{
    BufferFull = 1,
    TradeObserved = 2,
    FeedStopped = 3,
    TickerRemoved = 4,
    FeedFaulted = 5,
    ValueDateChanged = 6
}
