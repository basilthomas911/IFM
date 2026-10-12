using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal delegate LatestPriceResult64 LatestPriceQueryInvoker(
    string dataset,
    LatestPriceRequest request,
    uint timeoutMilliseconds);
