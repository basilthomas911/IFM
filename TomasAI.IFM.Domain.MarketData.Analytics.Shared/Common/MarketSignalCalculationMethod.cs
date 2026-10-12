using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Identifies how a market analytics value was calculated.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketSignalCalculationMethod : byte
{
    /// <summary>The calculation method is unknown.</summary>
    Unknown = 0,

    /// <summary>The signal was calculated from exact normalized trades.</summary>
    ExactTrades = 1,

    /// <summary>The signal was calculated from an IFM closed OHLCV observation.</summary>
    ClosedObservation = 2,

    /// <summary>The signal was calculated from a normalized historical provider aggregate.</summary>
    NormalizedHistoricalAggregate = 3
}
