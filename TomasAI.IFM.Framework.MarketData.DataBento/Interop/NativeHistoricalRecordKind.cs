using System.Runtime.InteropServices;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal enum NativeHistoricalRecordKind : uint
{
    Definition = 1,
    Ohlcv = 2,
    Trade = 3,
    Statistic = 4
}
