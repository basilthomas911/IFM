using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

/// <summary>
/// Adds optional option valuation state to the common ticker price snapshot.
/// </summary>
/// <param name="Price">The price snapshot or record price represented by this value.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct OptionTickerPriceSnapshot(
    TickerPriceSnapshot Price,
    OptionGreeksSnapshot? Greeks);
