using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal enum ProcessorCoreKind : byte
{
    Unknown = 0,
    Performance = 1,
    Efficiency = 2
}
