using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal enum NativeHistoricalSchema : uint
{
    Definition = 1,
    OhlcvOneMinute = 2,
    Trades = 3,
    Statistics = 4,
    OhlcvDaily = 5
}
