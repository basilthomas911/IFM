using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal interface ILatestPriceAdmissionClock
{
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);
    void Wait(object gate, TimeSpan timeout);
}
