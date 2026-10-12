using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new TickerInstrumentRegistration instance.</summary>
/// <param name="RequestedSymbol">The requested symbol.</param>
/// <param name="RawSymbol">The raw symbol.</param>
/// <param name="Instrument">The instrument.</param>
public sealed record TickerInstrumentRegistration(
    string RequestedSymbol,
    string RawSymbol,
    InstrumentKey Instrument);
