using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new OptionContractSelection instance.</summary>
/// <param name="RawSymbol">The raw symbol.</param>
/// <param name="Instrument">The instrument.</param>
/// <param name="Right">The right.</param>
public sealed record OptionContractSelection(
    string RawSymbol,
    InstrumentKey Instrument,
    OptionRightSelection Right = OptionRightSelection.None);
