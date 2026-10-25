using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Identifies the provider-neutral historical record shape requested for a series.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid for this existing serialized enum; preserve its existing values.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesAnalyticsHistoricalSchema : byte
{
    /// <summary>One-minute OHLCV observations.</summary>
    OhlcvOneMinute = 1,
    /// <summary>Exact normalized trades.</summary>
    Trades = 2,
    /// <summary>Daily OHLCV observations.</summary>
    OhlcvDaily = 3
}
