using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Identifies provider-neutral input symbology.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published provider enum wire representation.")]
public enum HistoricalSymbology : byte
{
    /// <summary>No symbology was selected.</summary>
    Unknown = 0,
    /// <summary>An exact provider raw symbol.</summary>
    RawSymbol = 1,
    /// <summary>A provider continuous futures symbol.</summary>
    Continuous = 2,
    /// <summary>A provider instrument identifier.</summary>
    InstrumentId = 3
}
