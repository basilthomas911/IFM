using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
[Flags]
public enum OptionRightSelection : byte
{
    None = 0,
    Call = 1,
    Put = 2,
    Both = Call | Put
}
