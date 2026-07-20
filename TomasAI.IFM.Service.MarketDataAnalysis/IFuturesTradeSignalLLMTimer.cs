using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Service.MarketDataAnalytics
{
    public  interface IFuturesTradeSignalLLMTimer
    {
        Task StartAsync(FuturesTradeSignalLLMStartedEvent e, Action<FuturesTradeSignalId> timerAction);
        Task StopAsync(FuturesTradeSignalLLMStoppedEvent e);
    }
}
