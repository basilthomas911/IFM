using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal enum NativeContractQueryKind : uint
{
    Exact = 1,
    Ticker = 2,
    InstrumentId = 3,
    Dataset = 4
}
