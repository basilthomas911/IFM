using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new InstrumentKey instance.</summary>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct InstrumentKey(
    ushort PublisherId,
    uint InstrumentId);
