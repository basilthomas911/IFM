using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The published message or native ABI fixes the enum storage width; changing it would alter the wire contract.")]
[Flags]
public enum MarketDataKinds : byte
{
    None = 0,
    Quote = 1,
    Trade = 2,
    MboOrderUpdate = 4,
    Statistics = 8,
    SessionVolume = 16
}
