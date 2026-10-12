using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Identifies a provider-neutral historical market-data schema.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published provider enum wire representation.")]
public enum HistoricalDataSchema : byte
{
    /// <summary>No schema was selected.</summary>
    Unknown = 0,
    /// <summary>Instrument definitions and symbol mappings.</summary>
    Definition = 1,
    /// <summary>One-minute open, high, low, close, and volume records.</summary>
    OhlcvOneMinute = 2,
    /// <summary>Individual trade records.</summary>
    Trades = 3,
    /// <summary>Exchange statistics records.</summary>
    Statistics = 4,
    /// <summary>Daily open, high, low, close, and volume records.</summary>
    OhlcvDaily = 5
}
