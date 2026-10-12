using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new TickerSubscription instance.</summary>
/// <param name="Symbol">The symbol.</param>
/// <param name="InputSymbology">The input symbology.</param>
/// <param name="DataKinds">The data kinds.</param>
public readonly record struct TickerSubscription(
    string Symbol,
    DatabentoInputSymbology InputSymbology,
    MarketDataKinds DataKinds);
