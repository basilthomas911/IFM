using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    public  interface IFuturesRsiSignalTimer
    {
        Task StartAsync(FuturesRsiSignalStartedEvent e, Action<FuturesRsiSignalEntityId> timerAction);
        Task StopAsync(FuturesRsiSignalStoppedEvent e);
    }
}
