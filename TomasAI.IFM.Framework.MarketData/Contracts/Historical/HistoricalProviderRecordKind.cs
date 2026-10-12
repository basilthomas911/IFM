using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Historical;

/// <summary>Identifies the normalized record shape returned by a historical reader.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserves the published provider enum wire representation.")]
public enum HistoricalProviderRecordKind : byte
{
    /// <summary>The record kind is unknown.</summary>
    Unknown = 0,
    /// <summary>An instrument definition or mapping.</summary>
    Definition = 1,
    /// <summary>A one-minute OHLCV observation.</summary>
    Ohlcv = 2,
    /// <summary>An individual trade.</summary>
    Trade = 3,
    /// <summary>An exchange statistic.</summary>
    Statistic = 4
}
