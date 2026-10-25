using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

/// <summary>
/// Describes the provider-neutral lifecycle action represented by a normalized trade.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published enum numeric wire representation.")]
public enum NormalizedTradeAction : byte
{
    /// <summary>The source action was absent or unsupported.</summary>
    Unknown = 0,

    /// <summary>A new trade was reported.</summary>
    New = 1,

    /// <summary>An existing trade was modified or corrected.</summary>
    Change = 2,

    /// <summary>An existing trade was cancelled.</summary>
    Cancel = 3,

    /// <summary>The source explicitly reported a correction.</summary>
    Correct = 4,

    /// <summary>The source cleared its current trade state.</summary>
    Clear = 5,

    /// <summary>The source explicitly supplied no action.</summary>
    None = 6
}
