using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "This published domain collection marker retains the existing collection interface contract.")]
[Flags]
internal enum NativeHistoricalFlags : uint
{
    None = 0,
    Synthetic = 1
}
