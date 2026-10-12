using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserve the published domain collection type name used by consumers.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "This published domain collection marker retains the existing collection interface contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
[Flags]
public enum LatestPriceResultFlags : byte
{
    None = 0,
    BidValid = 1,
    AskValid = 2,
    TradeValid = 4,
    ReplayContributed = 8,
    FinalRecordLive = 16
}
