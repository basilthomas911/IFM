using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Describes the provider-neutral aggressor side of a normalized trade.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
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
