using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "This published domain collection marker retains the existing collection interface contract.")]
[Flags]
internal enum NativeContractDetailFlags : uint
{
    None = 0,
    Found = 1,
    HasStrikePrice = 2,
    HasMinimumPriceIncrement = 4,
    HasExpiration = 8,
    HasActivation = 16,
    HasMaturityDate = 32,
    HasMultiplier = 64,
    HasMinimumPriceIncrementAmount = 128,
    HasMaturityWeek = 256
}
