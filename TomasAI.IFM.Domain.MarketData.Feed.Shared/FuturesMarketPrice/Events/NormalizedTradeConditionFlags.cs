using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Provides provider-neutral conditions retained from a normalized trade observation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "Preserves the existing externally referenced identity and named API contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserves the existing externally referenced type identity.")]
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
