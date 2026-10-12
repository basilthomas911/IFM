using System.Buffers;
using System.Globalization;
using MessagePack;
using MessagePack.Formatters;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum AssetTypeId : byte
{
    Unknown = 0,
    Futures = 1,
    FuturesOption = 2,
    Equity = 3,
    EquityOption = 4
}
