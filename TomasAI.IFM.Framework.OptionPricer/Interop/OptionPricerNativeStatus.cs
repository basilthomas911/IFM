using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.OptionPricer.Interop;

internal enum OptionPricerNativeStatus
{
    Ok = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    NoConvergence = 3,
    Panic = 4
}
